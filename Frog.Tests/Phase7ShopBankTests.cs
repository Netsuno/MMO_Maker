using System;
using System.Linq;
using System.Threading.Tasks;
using Frog.Server.Gameplay;
using Frog.Server.Models;
using Xunit;

namespace Frog.Tests;

public sealed class Phase7ShopBankTests
{
    [Fact]
    public async Task Buy_SpendGold_AddsItem()
    {
        var content = new Phase7PublishedContent();
        var chars = new InMemoryCharacterRepository();
        var inv = new InMemoryInventoryRepository();
        var bank = new InMemoryBankRepository();
        var charSvc = new CharacterGameplayService(chars, content, inv);
        var economy = new InMemoryEconomyTransactionRepository(chars, inv, bank);
        var shopSvc = new ShopBankGameplayService(content, content, chars, inv, bank, economy);
        var created = await charSvc.CreateAsync(Guid.NewGuid(), "Buyer", Phase7ContentSeed.DefaultClassId);
        var session = new Session { Id = Guid.NewGuid(), Username = "buyer" };
        session.ApplyFromCharacter(created.Character!);
        session.Gold = 500;
        await chars.SaveAsync(session.ToCharacterPatch(created.Character!));

        var result = await shopSvc.TryBuyAsync(
            session,
            Phase7ContentSeed.DefaultShopId,
            Phase7ContentSeed.DefaultItemId,
            1);
        Assert.True(result.Success);
        Assert.True(session.Gold < 500);
        var inventory = await inv.GetAsync(session.RequireCharacterGuid());
        Assert.Contains(inventory.Slots, s => s.ItemId == Phase7ContentSeed.DefaultItemId);
    }

    [Fact]
    public async Task BankDepositWithdraw_RoundTrip()
    {
        var content = new Phase7PublishedContent();
        var chars = new InMemoryCharacterRepository();
        var inv = new InMemoryInventoryRepository();
        var bank = new InMemoryBankRepository();
        var charSvc = new CharacterGameplayService(chars, content, inv);
        var economy = new InMemoryEconomyTransactionRepository(chars, inv, bank);
        var shopSvc = new ShopBankGameplayService(content, content, chars, inv, bank, economy);
        var created = await charSvc.CreateAsync(Guid.NewGuid(), "Banker", Phase7ContentSeed.DefaultClassId);
        await inv.TryAddAsync(created.Character!.Id, Phase7ContentSeed.DefaultItemId, 3, 20);
        var session = new Session { Id = Guid.NewGuid(), Username = "banker" };
        session.ApplyFromCharacter(created.Character);

        var deposit = await shopSvc.TryDepositItemAsync(session, 0, 2);
        Assert.True(deposit.Success);
        var withdraw = await shopSvc.TryWithdrawItemAsync(session, 0, 1);
        Assert.True(withdraw.Success);
        var bankSnap = await bank.GetAsync(session.RequireCharacterGuid());
        Assert.Contains(bankSnap.Slots, s => s.ItemId == Phase7ContentSeed.DefaultItemId && s.Quantity == 1);
    }
}
