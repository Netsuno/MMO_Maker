using System.Buffers.Binary;
using System.Text;
using Frog.Application.Gameplay;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Gameplay;
using Frog.Core.Protocol;
using Frog.Server.Gameplay;
using Frog.Server.Models;

namespace Frog.Server.Network;

public sealed partial class PacketDispatcher
{
    private static bool UsesAccountGameplay(Session session) => session.AccountId != Guid.Empty;

    private async Task SendGameplaySnapshotsAsync(
        ClientSession clientSession,
        Session session,
        CancellationToken cancellationToken)
    {
        await SendCombatStateAsync(clientSession, session, cancellationToken);
        await SendInventorySnapshotAsync(clientSession, session, cancellationToken);
        await SendBankSnapshotAsync(clientSession, session, cancellationToken);
        await SendGroundItemsSnapshotAsync(clientSession, session, cancellationToken);
    }

    private Task SendCombatStateAsync(ClientSession clientSession, Session session, CancellationToken cancellationToken)
        => _packetSender.SendCombatStateAsync(
            clientSession,
            session.Level,
            session.Experience,
            session.Hp,
            session.MaxHp,
            session.Mp,
            session.MaxMp,
            session.Gold,
            session.IsDead,
            cancellationToken);

    private async Task SendInventorySnapshotAsync(
        ClientSession clientSession,
        Session session,
        CancellationToken cancellationToken)
    {
        if (!session.HasActiveCharacter())
        {
            return;
        }

        var inv = await _inventoryGameplay.GetInventoryAsync(session.RequireCharacterGuid(), cancellationToken)
            .ConfigureAwait(false);
        var wire = new InventorySnapshotWire
        {
            EquippedWeaponItemId = session.EquippedWeaponItemId,
            EquippedArmorItemId = session.EquippedArmorItemId,
            Slots = inv.Slots.Select(s => new InventorySlotWire
            {
                SlotIndex = s.SlotIndex,
                ItemId = s.ItemId,
                Quantity = s.Quantity,
            }).ToArray(),
        };
        await _packetSender.SendInventorySnapshotAsync(clientSession, wire, cancellationToken);
    }

    private async Task SendBankSnapshotAsync(
        ClientSession clientSession,
        Session session,
        CancellationToken cancellationToken)
    {
        if (!session.HasActiveCharacter())
        {
            return;
        }

        var characterId = session.RequireCharacterGuid();
        var bank = await _shopBankGameplay.GetBankAsync(characterId, cancellationToken).ConfigureAwait(false);
        var wire = new BankSnapshotWire
        {
            BankGold = await _shopBankGameplay.GetBankGoldAsync(characterId, cancellationToken).ConfigureAwait(false),
            Slots = bank.Slots.Select(s => new BankSlotWire
            {
                SlotIndex = s.SlotIndex,
                ItemId = s.ItemId,
                Quantity = s.Quantity,
            }).ToArray(),
        };
        await _packetSender.SendBankSnapshotAsync(clientSession, wire, cancellationToken);
    }

    private async Task SendGroundItemsSnapshotAsync(
        ClientSession clientSession,
        Session session,
        CancellationToken cancellationToken)
    {
        var items = await _inventoryGameplay.ListGroundOnMapAsync(session.CurrentMapId, cancellationToken)
            .ConfigureAwait(false);
        var wire = items.Select(i => new GroundItemWire
        {
            GroundItemId = i.Id,
            ItemId = i.ItemId,
            Quantity = i.Quantity,
            PixelX = i.PixelX,
            PixelY = i.PixelY,
        }).ToArray();
        await _packetSender.SendGroundItemsSnapshotAsync(clientSession, session.CurrentMapId, wire, cancellationToken);
    }

    private async Task BroadcastPositionAfterSelectAsync(
        ClientSession clientSession,
        Session session,
        int mapIdBeforeSelect,
        CancellationToken cancellationToken)
    {
        foreach (var targetClient in _clientRegistry.GetAllAuthenticatedClients())
        {
            await _packetSender.SendPositionUpdateAsync(
                targetClient,
                session.Username,
                session.CurrentMapId,
                session.PixelX,
                session.PixelY,
                cancellationToken);
        }

        if (session.CurrentMapId != mapIdBeforeSelect)
        {
            ReleasePageTriggerForPreviousMap(session, mapIdBeforeSelect);
        }

        await TryFirePageMapEventsAsync(clientSession, session, cancellationToken);
    }

    private async Task HandleEquipRequestAsync(
        ClientSession clientSession,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        if (!TryGetActiveSession(clientSession, out var session))
        {
            await _packetSender.SendErrorAsync(clientSession, "Authentification requise.", cancellationToken);
            return;
        }

        if (payload.Length != 1)
        {
            await _packetSender.SendEquipResultAsync(clientSession, false, "EquipRequest: 1 octet attendu.", cancellationToken);
            return;
        }

        var result = await _inventoryGameplay.TryEquipAsync(session, payload.Span[0], cancellationToken).ConfigureAwait(false);
        await _packetSender.SendEquipResultAsync(clientSession, result.Success, result.Message, cancellationToken);
        if (result.Success)
        {
            await SendInventorySnapshotAsync(clientSession, session, cancellationToken);
        }
    }

    private async Task HandleUnequipRequestAsync(
        ClientSession clientSession,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        if (!TryGetActiveSession(clientSession, out var session))
        {
            await _packetSender.SendErrorAsync(clientSession, "Authentification requise.", cancellationToken);
            return;
        }

        if (payload.Length != 1 || !Enum.IsDefined(typeof(EquipmentSlotKind), payload.Span[0]))
        {
            await _packetSender.SendUnequipResultAsync(clientSession, false, "UnequipRequest invalide.", cancellationToken);
            return;
        }

        var slot = (EquipmentSlotKind)payload.Span[0];
        var result = await _inventoryGameplay.TryUnequipAsync(session, slot, cancellationToken).ConfigureAwait(false);
        await _packetSender.SendUnequipResultAsync(clientSession, result.Success, result.Message, cancellationToken);
        if (result.Success)
        {
            await SendInventorySnapshotAsync(clientSession, session, cancellationToken);
        }
    }

    private async Task HandleDropItemRequestAsync(
        ClientSession clientSession,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        if (!TryGetActiveSession(clientSession, out var session))
        {
            await _packetSender.SendErrorAsync(clientSession, "Authentification requise.", cancellationToken);
            return;
        }

        if (payload.Length != 1 + sizeof(int))
        {
            await _packetSender.SendDropItemResultAsync(clientSession, false, "DropItemRequest invalide.", cancellationToken);
            return;
        }

        var slot = payload.Span[0];
        var qty = BinaryPrimitives.ReadInt32LittleEndian(payload.Span.Slice(1));
        var result = await _inventoryGameplay.TryDropAsync(session, slot, qty, cancellationToken).ConfigureAwait(false);
        await _packetSender.SendDropItemResultAsync(clientSession, result.Success, result.Message, cancellationToken);
        if (result.Success)
        {
            await SendInventorySnapshotAsync(clientSession, session, cancellationToken);
            await SendGroundItemsSnapshotAsync(clientSession, session, cancellationToken);
        }
    }

    private async Task HandlePickupItemRequestAsync(
        ClientSession clientSession,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        if (!TryGetActiveSession(clientSession, out var session))
        {
            await _packetSender.SendErrorAsync(clientSession, "Authentification requise.", cancellationToken);
            return;
        }

        if (payload.Length != 16)
        {
            await _packetSender.SendPickupItemResultAsync(clientSession, false, "PickupItemRequest invalide.", cancellationToken);
            return;
        }

        var groundId = new Guid(payload.Span);
        var result = await _inventoryGameplay.TryPickupAsync(session, groundId, cancellationToken).ConfigureAwait(false);
        await _packetSender.SendPickupItemResultAsync(clientSession, result.Success, result.Message, cancellationToken);
        if (result.Success)
        {
            await SendInventorySnapshotAsync(clientSession, session, cancellationToken);
            await SendGroundItemsSnapshotAsync(clientSession, session, cancellationToken);
        }
    }

    private async Task HandleSpellCastRequestAsync(
        ClientSession clientSession,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        if (!TryGetActiveSession(clientSession, out var session))
        {
            await _packetSender.SendErrorAsync(clientSession, "Authentification requise.", cancellationToken);
            return;
        }

        if (!TryParseSpellCastRequest(payload.Span, out var spellId, out var targetName))
        {
            await _packetSender.SendSpellCastResultAsync(clientSession, false, "SpellCastRequest invalide.", cancellationToken);
            return;
        }

        var result = await _combatGameplay.TryCastSpellAsync(session, spellId, targetName, cancellationToken)
            .ConfigureAwait(false);
        await _packetSender.SendSpellCastResultAsync(clientSession, result.Success, result.Message, cancellationToken);
        if (!result.Success)
        {
            return;
        }

        if (result.MonsterKilled && result.ExperienceGained > 0)
        {
            await _packetSender.SendExperienceGainAsync(
                clientSession,
                result.ExperienceGained,
                session.Level,
                session.Experience,
                cancellationToken);
        }

        await SendCombatStateAsync(clientSession, session, cancellationToken);
    }

    private async Task HandleShopBuyRequestAsync(
        ClientSession clientSession,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        if (!TryGetActiveSession(clientSession, out var session))
        {
            await _packetSender.SendErrorAsync(clientSession, "Authentification requise.", cancellationToken);
            return;
        }

        if (!TryParseShopBuyRequest(payload.Span, out var shopId, out var itemId, out var quantity, out var requestId))
        {
            await _packetSender.SendShopBuyResultAsync(clientSession, false, "ShopBuyRequest invalide.", cancellationToken);
            return;
        }

        if (requestId == Guid.Empty)
        {
            await _packetSender.SendShopBuyResultAsync(clientSession, false, "RequestId requis.", cancellationToken);
            return;
        }

        var result = await _shopBankGameplay.TryBuyAsync(session, shopId, itemId, quantity, requestId, cancellationToken)
            .ConfigureAwait(false);
        await _packetSender.SendShopBuyResultAsync(clientSession, result.Success, result.Message, cancellationToken);
        if (result.Success)
        {
            await SendInventorySnapshotAsync(clientSession, session, cancellationToken);
            await SendCombatStateAsync(clientSession, session, cancellationToken);
        }
    }

    private async Task HandleShopSellRequestAsync(
        ClientSession clientSession,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        if (!TryGetActiveSession(clientSession, out var session))
        {
            await _packetSender.SendErrorAsync(clientSession, "Authentification requise.", cancellationToken);
            return;
        }

        if (!TryParseShopSellRequest(payload.Span, out var slot, out var qty, out var requestId))
        {
            await _packetSender.SendShopSellResultAsync(clientSession, false, "ShopSellRequest invalide.", cancellationToken);
            return;
        }

        if (requestId == Guid.Empty)
        {
            await _packetSender.SendShopSellResultAsync(clientSession, false, "RequestId requis.", cancellationToken);
            return;
        }

        var result = await _shopBankGameplay.TrySellAsync(session, slot, qty, requestId, cancellationToken).ConfigureAwait(false);
        await _packetSender.SendShopSellResultAsync(clientSession, result.Success, result.Message, cancellationToken);
        if (result.Success)
        {
            await SendInventorySnapshotAsync(clientSession, session, cancellationToken);
            await SendCombatStateAsync(clientSession, session, cancellationToken);
        }
    }

    private async Task HandleBankDepositRequestAsync(
        ClientSession clientSession,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        if (!TryGetActiveSession(clientSession, out var session))
        {
            await _packetSender.SendErrorAsync(clientSession, "Authentification requise.", cancellationToken);
            return;
        }

        if (TryParseBankDepositItemRequest(payload.Span, out var depositSlot, out var depositQty, out var depositRequestId))
        {
            if (depositRequestId == Guid.Empty)
            {
                await _packetSender.SendBankDepositResultAsync(clientSession, false, "RequestId requis.", cancellationToken);
                return;
            }

            var result = await _shopBankGameplay.TryDepositItemAsync(
                    session, depositSlot, depositQty, depositRequestId, cancellationToken)
                .ConfigureAwait(false);
            await _packetSender.SendBankDepositResultAsync(clientSession, result.Success, result.Message, cancellationToken);
            if (result.Success)
            {
                await SendInventorySnapshotAsync(clientSession, session, cancellationToken);
                await SendBankSnapshotAsync(clientSession, session, cancellationToken);
            }

            return;
        }

        if (TryParseBankDepositGoldRequest(payload.Span, out var depositGold, out var depositGoldRequestId))
        {
            if (depositGoldRequestId == Guid.Empty)
            {
                await _packetSender.SendBankDepositResultAsync(clientSession, false, "RequestId requis.", cancellationToken);
                return;
            }

            var goldResult = await _shopBankGameplay.TryDepositGoldAsync(
                    session, depositGold, depositGoldRequestId, cancellationToken)
                .ConfigureAwait(false);
            await _packetSender.SendBankDepositResultAsync(clientSession, goldResult.Success, goldResult.Message, cancellationToken);
            if (goldResult.Success)
            {
                await SendCombatStateAsync(clientSession, session, cancellationToken);
                await SendBankSnapshotAsync(clientSession, session, cancellationToken);
            }

            return;
        }

        await _packetSender.SendBankDepositResultAsync(clientSession, false, "BankDepositRequest invalide.", cancellationToken);
    }

    private async Task HandleBankWithdrawRequestAsync(
        ClientSession clientSession,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        if (!TryGetActiveSession(clientSession, out var session))
        {
            await _packetSender.SendErrorAsync(clientSession, "Authentification requise.", cancellationToken);
            return;
        }

        if (TryParseBankWithdrawItemRequest(payload.Span, out var withdrawSlot, out var withdrawQty, out var withdrawRequestId))
        {
            if (withdrawRequestId == Guid.Empty)
            {
                await _packetSender.SendBankWithdrawResultAsync(clientSession, false, "RequestId requis.", cancellationToken);
                return;
            }

            var result = await _shopBankGameplay.TryWithdrawItemAsync(
                    session, withdrawSlot, withdrawQty, withdrawRequestId, cancellationToken)
                .ConfigureAwait(false);
            await _packetSender.SendBankWithdrawResultAsync(clientSession, result.Success, result.Message, cancellationToken);
            if (result.Success)
            {
                await SendInventorySnapshotAsync(clientSession, session, cancellationToken);
                await SendBankSnapshotAsync(clientSession, session, cancellationToken);
            }

            return;
        }

        if (TryParseBankWithdrawGoldRequest(payload.Span, out var withdrawGold, out var withdrawGoldRequestId))
        {
            if (withdrawGoldRequestId == Guid.Empty)
            {
                await _packetSender.SendBankWithdrawResultAsync(clientSession, false, "RequestId requis.", cancellationToken);
                return;
            }

            var goldResult = await _shopBankGameplay.TryWithdrawGoldAsync(
                    session, withdrawGold, withdrawGoldRequestId, cancellationToken)
                .ConfigureAwait(false);
            await _packetSender.SendBankWithdrawResultAsync(clientSession, goldResult.Success, goldResult.Message, cancellationToken);
            if (goldResult.Success)
            {
                await SendCombatStateAsync(clientSession, session, cancellationToken);
                await SendBankSnapshotAsync(clientSession, session, cancellationToken);
            }

            return;
        }

        await _packetSender.SendBankWithdrawResultAsync(clientSession, false, "BankWithdrawRequest invalide.", cancellationToken);
    }

    private async Task HandleRespawnRequestAsync(
        ClientSession clientSession,
        ReadOnlyMemory<byte> payload,
        CancellationToken cancellationToken)
    {
        if (!TryGetActiveSession(clientSession, out var session))
        {
            await _packetSender.SendErrorAsync(clientSession, "Authentification requise.", cancellationToken);
            return;
        }

        if (!payload.IsEmpty)
        {
            await _packetSender.SendRespawnResultAsync(clientSession, false, "RespawnRequest: corps vide attendu.", cancellationToken);
            return;
        }

        var result = await _combatGameplay.TryRespawnAsync(session, cancellationToken).ConfigureAwait(false);
        await _packetSender.SendRespawnResultAsync(clientSession, result.Success, result.Message, cancellationToken);
        if (result.Success)
        {
            await SendCombatStateAsync(clientSession, session, cancellationToken);
            foreach (var targetClient in _clientRegistry.GetAllAuthenticatedClients())
            {
                await _packetSender.SendPositionUpdateAsync(
                    targetClient,
                    session.Username,
                    session.CurrentMapId,
                    session.PixelX,
                    session.PixelY,
                    cancellationToken);
            }
        }
    }

    public static bool TryParseSpellCastRequest(ReadOnlySpan<byte> payload, out Guid spellId, out string? targetName)
    {
        spellId = Guid.Empty;
        targetName = null;
        if (payload.Length < 16)
        {
            return false;
        }

        spellId = new Guid(payload.Slice(0, 16));
        if (payload.Length == 16)
        {
            return spellId != Guid.Empty;
        }

        if (payload.Length < 17)
        {
            return false;
        }

        var len = payload[16];
        if (len is 0 or > ChatProtocolLimits.MaxUsernameUtf8Bytes || payload.Length != 17 + len)
        {
            return false;
        }

        targetName = Encoding.UTF8.GetString(payload.Slice(17, len));
        return spellId != Guid.Empty;
    }

    public static bool TryParseShopBuyRequest(ReadOnlySpan<byte> payload, out Guid shopId, out Guid itemId, out int quantity, out Guid requestId)
    {
        shopId = Guid.Empty;
        itemId = Guid.Empty;
        quantity = 0;
        requestId = Guid.Empty;
        if (payload.Length != 16 + 16 + 4 + 16)
        {
            return false;
        }

        shopId = new Guid(payload.Slice(0, 16));
        itemId = new Guid(payload.Slice(16, 16));
        quantity = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(32));
        requestId = new Guid(payload.Slice(36));
        return shopId != Guid.Empty && itemId != Guid.Empty && quantity > 0 && requestId != Guid.Empty;
    }

    public static bool TryParseShopSellRequest(ReadOnlySpan<byte> payload, out byte slot, out int quantity, out Guid requestId)
    {
        slot = 0;
        quantity = 0;
        requestId = Guid.Empty;
        if (payload.Length != 1 + 4 + 16)
        {
            return false;
        }

        slot = payload[0];
        quantity = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(1));
        requestId = new Guid(payload.Slice(5));
        return quantity > 0 && requestId != Guid.Empty;
    }

    public static bool TryParseBankDepositItemRequest(ReadOnlySpan<byte> payload, out byte slot, out int quantity, out Guid requestId)
    {
        slot = 0;
        quantity = 0;
        requestId = Guid.Empty;
        if (payload.Length != 1 + sizeof(int) + 16)
        {
            return false;
        }

        slot = payload[0];
        quantity = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(1));
        requestId = new Guid(payload.Slice(1 + sizeof(int)));
        return quantity > 0 && requestId != Guid.Empty;
    }

    public static bool TryParseBankDepositGoldRequest(ReadOnlySpan<byte> payload, out int amount, out Guid requestId)
    {
        amount = 0;
        requestId = Guid.Empty;
        if (payload.Length != sizeof(int) + 16)
        {
            return false;
        }

        amount = BinaryPrimitives.ReadInt32LittleEndian(payload);
        requestId = new Guid(payload.Slice(sizeof(int)));
        return amount > 0 && requestId != Guid.Empty;
    }

    public static bool TryParseBankWithdrawItemRequest(ReadOnlySpan<byte> payload, out byte slot, out int quantity, out Guid requestId)
    {
        slot = 0;
        quantity = 0;
        requestId = Guid.Empty;
        if (payload.Length != 1 + sizeof(int) + 16)
        {
            return false;
        }

        slot = payload[0];
        quantity = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(1));
        requestId = new Guid(payload.Slice(1 + sizeof(int)));
        return quantity > 0 && requestId != Guid.Empty;
    }

    public static bool TryParseBankWithdrawGoldRequest(ReadOnlySpan<byte> payload, out int amount, out Guid requestId)
    {
        amount = 0;
        requestId = Guid.Empty;
        if (payload.Length != sizeof(int) + 16)
        {
            return false;
        }

        amount = BinaryPrimitives.ReadInt32LittleEndian(payload);
        requestId = new Guid(payload.Slice(sizeof(int)));
        return amount > 0 && requestId != Guid.Empty;
    }
}
