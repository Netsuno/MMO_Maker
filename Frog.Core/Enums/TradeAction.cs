namespace Frog.Core.Enums;

public enum TradeAction : byte
{
    Invite = 1,
    Accept = 2,
    Decline = 3,
    Cancel = 4,
    SetOffer = 5,
    Confirm = 6,
    Unconfirm = 7
}

public enum TradeStatus : byte
{
    Inviting = 1,
    Open = 2,
    Committed = 3,
    Cancelled = 4
}
