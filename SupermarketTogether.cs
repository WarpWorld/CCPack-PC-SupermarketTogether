using CrowdControl.Common;
using System.ComponentModel;
using ConnectorType = CrowdControl.Common.ConnectorType;

namespace CrowdControl.Games.Packs.SupermarketTogether
{

    public class SupermarketTogether : SimpleTCPPack
    {
        public override string Host => "127.0.0.1";

        public override ushort Port => 51337;

        public override ISimpleTCPPack.MessageFormat MessageFormat => ISimpleTCPPack.MessageFormat.CrowdControlLegacy;

        public SupermarketTogether(UserRecord player, Func<CrowdControlBlock, bool> responseHandler, Action<object> statusUpdateHandler) : base(player, responseHandler, statusUpdateHandler) { }

        public override Game Game { get; } = new("Supermarket Together", "SupermarketTogether", "PC", ConnectorType.SimpleTCPServerConnector);

        public override EffectList Effects => new List<Effect>
        {
            new Effect("Turn Lights On", "lighton") { Description = "Toggle Lights On", Category = "Misc"},
            
            
            new Effect("Give $100", "money100") { Description = "Gives 100 of the Selected Currency", Category = "Money"},

            new Effect("Give $1000", "money1000"){ Description = "Gives 1000 of the Selected Currency", Category = "Money"},

            new Effect("Give 1 Franchise Point", "give1fp"){ Description = "Gives 1 Franchise Point to the Player(s)", Category = "Stats"},


            new Effect("Send Pasta", "give_0"){ Description = "Spawn an Item", Category = "Items"},
            new Effect("Send Water Bottles", "give_1"){ Description = "Spawn an Item", Category = "Items"},
            new Effect("Send Honey Cereal", "give_2"){ Description = "Spawn an Item", Category = "Items"},
            new Effect("Send Rice", "give_3"){ Description = "Spawn an Item", Category = "Items"},
            new Effect("Send Salt", "give_4"){ Description = "Spawn an Item", Category = "Items"},
            new Effect("Send Sugar", "give_5"){ Description = "Spawn an Item", Category = "Items"},
            new Effect("Send Margarine", "give_6"){ Description = "Spawn an Item", Category = "Items"},
            new Effect("Send Flour", "give_7"){ Description = "Spawn an Item", Category = "Items"},
            new Effect("Send Apple Juice", "give_8"){ Description = "Spawn an Item", Category = "Items"},
            new Effect("Send Olive Oil", "give_9"){ Description = "Spawn an Item", Category = "Items"},
            new Effect("Send Ketchup", "give_10"){ Description = "Spawn an Item", Category = "Items"},
            new Effect("Send Sliced Bread", "give_11"){ Description = "Spawn an Item", Category = "Items"},
            new Effect("Send Pepper", "give_12"){ Description = "Spawn an Item", Category = "Items"},
            new Effect("Send Emmental Cheese", "give_43"){ Description = "Spawn an Item", Category = "Items"},
            new Effect("Send Gruyere Cheese", "give_44"){ Description = "Spawn an Item", Category = "Items"},
            new Effect("Send Skimmed Cheese", "give_45"){ Description = "Spawn an Item", Category = "Items"},
            new Effect("Send Fruit Yoghurt", "give_46"){ Description = "Spawn an Item", Category = "Items"},
            new Effect("Send Vanilla Yoghurt", "give_47"){ Description = "Spawn an Item", Category = "Items"},
            new Effect("Send Milk Brick", "give_48"){ Description = "Spawn an Item", Category = "Items"},
            new Effect("Send BBQ Pizza", "give_61"){ Description = "Spawn an Item", Category = "Items"},
            new Effect("Send Fondue", "give_62"){ Description = "Spawn an Item", Category = "Items"},
            new Effect("Send Crocanti Ham", "give_63"){ Description = "Spawn an Item", Category = "Items"},
            new Effect("Send Ham & Cheese Crepe", "give_64"){ Description = "Spawn an Item", Category = "Items"},
            new Effect("Send French Fries", "give_65"){ Description = "Spawn an Item", Category = "Items"},
            new Effect("Send Crispy Potato", "give_66"){ Description = "Spawn an Item", Category = "Items"},
            new Effect("Send Green Beans", "give_67"){ Description = "Spawn an Item", Category = "Items"},
            new Effect("Send Green Tea", "give_140"){ Description = "Spawn an Item", Category = "Items"},
            new Effect("Send Lemon Tea", "give_141"){ Description = "Spawn an Item", Category = "Items"},
            new Effect("Send Black Tea", "give_142"){ Description = "Spawn an Item", Category = "Items"},
            new Effect("Send Peppermint", "give_143"){ Description = "Spawn an Item", Category = "Items"},
            new Effect("Send Mint", "give_144"){ Description = "Spawn an Item", Category = "Items"},
            new Effect("Send Valerian", "give_145"){ Description = "Spawn an Item", Category = "Items"},
        };
    }
}
