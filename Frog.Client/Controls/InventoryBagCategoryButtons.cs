using Frog.Client.UI;
using Frog.Core.Gameplay;

namespace Frog.Client.Controls;

/// <summary>Boutons exclusifs Arme / Armure / Objet. Un second clic réaffiche tout le sac.</summary>
internal sealed class InventoryBagCategoryButtons
{
    private readonly Button _weapon;
    private readonly Button _armor;
    private readonly Button _item;

    public InventoryBagCategoryButtons()
    {
        _weapon = Create(InventoryBagCategory.Weapon);
        _armor = Create(InventoryBagCategory.Armor);
        _item = Create(InventoryBagCategory.Item);
    }

    public event Action? Changed;

    public InventoryBagCategory Category { get; private set; } = InventoryBagCategory.All;

    public Button WeaponButton => _weapon;

    public Button ArmorButton => _armor;

    public Button ItemButton => _item;

    internal void ClickForTest(InventoryBagCategory category) => ButtonFor(category).PerformClick();

    private Button Create(InventoryBagCategory category)
    {
        var label = InventoryBagFilter.Label(category);
        var button = new Button
        {
            Text = label,
            AutoSize = true,
            Margin = new Padding(2),
            AccessibleName = "Filtre " + label,
        };
        button.Click += (_, _) =>
        {
            Category = Category == category ? InventoryBagCategory.All : category;
            ApplyChrome();
            Changed?.Invoke();
        };
        return button;
    }

    private Button ButtonFor(InventoryBagCategory category) => category switch
    {
        InventoryBagCategory.Weapon => _weapon,
        InventoryBagCategory.Armor => _armor,
        InventoryBagCategory.Item => _item,
        _ => throw new ArgumentOutOfRangeException(
            nameof(category),
            category,
            "Choisissez Arme, Armure ou Objet."),
    };

    private void ApplyChrome()
    {
        UiTheme.StyleButton(_weapon);
        UiTheme.StyleButton(_armor);
        UiTheme.StyleButton(_item);
        if (Category == InventoryBagCategory.All)
        {
            return;
        }

        var selected = ButtonFor(Category);
        selected.BackColor = UiTheme.BgRowSelected;
        selected.ForeColor = UiTheme.TextGold;
    }
}
