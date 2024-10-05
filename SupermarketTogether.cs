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
            new Effect("Complain about Filth", "complain_filth") { Description = "Customers Complain About Filth", Category = "Misc"},
            new Effect("Spawn New Customer", "spawn_customer") { Description = "Spawn a customer!", Category = "Misc"},
            new Effect("Spawn Employee", "spawn_employee"){ Description = "Spawns a new Employee if Available", Category = "Misc"},
            new Effect("Spawn Trash", "spawn_trash"){ Description = "Spawn some trash!", Category = "Misc"},


            new Effect("Jail Player", "jailplayer"){ Description = "Put the player in jail!", Category = "Player"},
            new Effect("Force Math", "forcemath"){ Description = "Force the player to do the math when giving change!", Category = "Register"},



            new Effect("Open Supermarket", "open_super"){ Description = "Open the Player Store!", Category = "Misc"},
            new Effect("Store Name [CrowdControlStore]", "storename_1"){ Description = "Rename the Player Store!", Category = "Misc"},
            new Effect("Store Name [Streamer Megastore]", "storename_2"){ Description = "Rename the Player Store!", Category = "Misc"},
            new Effect("Store Name [WarpWorld Store]", "storename_3"){ Description = "Rename the Player Store!", Category = "Misc"},

            new Effect("Give $100", "givemoney_100") { Description = "Gives 100 of the Selected Currency", Category = "Money"},
            new Effect("Give $1000", "givemoney_1000"){ Description = "Gives 1000 of the Selected Currency", Category = "Money"},
            new Effect("Give $10000", "givemoney_10000") { Description = "Gives 10000 of the Selected Currency", Category = "Money"},

            new Effect("Take $100", "takemoney_100"){ Description = "Takes 100 of the Selected Currency", Category = "Money"},
            new Effect("Take $1000", "takemoney_1000"){ Description = "Takes 1000 of the Selected Currency", Category = "Money"},
            new Effect("Take $1000", "takemoney_10000"){ Description = "Takes 10000 of the Selected Currency", Category = "Money"},

            new Effect("Give 1 Franchise Point", "give1fp"){ Description = "Gives 1 Franchise Point to the Player(s)", Category = "Stats"},


            new Effect("Send Pasta", "give_0"){ Description = "Spawn a box of Pasta for the Player", Category = "Items"},
            new Effect("Send Water Bottles", "give_1"){ Description = "Spawn Water Bottles for the Player", Category = "Items"},
            new Effect("Send Honey Cereal", "give_2"){ Description = "Spawn Honey Cereal for the Player", Category = "Items"},
            new Effect("Send Rice", "give_3"){ Description = "Spawn Rice for the Player", Category = "Items"},
            new Effect("Send Salt", "give_4"){ Description = "Spawn Salt for the Player", Category = "Items"},
            new Effect("Send Sugar", "give_5"){ Description = "Spawn Sugar for the Player", Category = "Items"},
            new Effect("Send Margarine", "give_6"){ Description = "Spawn Margarine for the player", Category = "Items"},
            new Effect("Send Flour", "give_7"){ Description = "Spawn Flour for the player", Category = "Items"},
            new Effect("Send Apple Juice", "give_8"){ Description = "Spawn Apple Juice for the player", Category = "Items"},
            new Effect("Send Olive Oil", "give_9"){ Description = "Spawn Olive Oil for the player", Category = "Items"},
            new Effect("Send Ketchup", "give_10"){ Description = "Spawn Ketchup for the player", Category = "Items"},
            new Effect("Send Sliced Bread", "give_11"){ Description = "Spawn Bread for the Player", Category = "Items"},
            new Effect("Send Pepper", "give_12"){ Description = "Spawn Pepper for the player", Category = "Items"},
            new Effect("Send Orange Juice", "give_13"){ Description = "Spawn Orange Juice for the player", Category = "Items"},
            new Effect("Send Emmental Cheese", "give_43"){ Description = "Spawn Emmental Cheese for the player", Category = "Items"},
            new Effect("Send Gruyere Cheese", "give_44"){ Description = "Spawn Gruyere Cheese for the Player", Category = "Items"},
            new Effect("Send Skimmed Cheese", "give_45"){ Description = "Spawn Skimmed Cheese for the player", Category = "Items"},
            new Effect("Send Fruit Yoghurt", "give_46"){ Description = "Spawn Fruit Yoghurt for the player", Category = "Items"},
            new Effect("Send Vanilla Yoghurt", "give_47"){ Description = "Spawn Vanilla Yoghurt for the player", Category = "Items"},
            new Effect("Send Milk", "give_48"){ Description = "Spawn Milk for the player", Category = "Items"},
            new Effect("Send BBQ Pizza", "give_61"){ Description = "Spawn BBQ Pizza's for the player", Category = "Items"},
            new Effect("Send Fondue", "give_62"){ Description = "Spawn Fondue for the player", Category = "Items"},
            new Effect("Send Crocanti Ham", "give_63"){ Description = "Spawn Crocanti Ham for the Player", Category = "Items"},
            new Effect("Send Ham & Cheese Crepe", "give_64"){ Description = "Spawn Ham and Cheese Crepes for the player", Category = "Items"},
            new Effect("Send French Fries", "give_65"){ Description = "Spawn French Fries for the player", Category = "Items"},
            new Effect("Send Crispy Potato", "give_66"){ Description = "Spawn Crispy Potatoes for the player", Category = "Items"},
            new Effect("Send Green Beans", "give_67"){ Description = "Spawn Green Beans for the player", Category = "Items"},
            new Effect("Send Green Tea", "give_140"){ Description = "Spawn Green Tea for the player", Category = "Items"},
            new Effect("Send Lemon Tea", "give_141"){ Description = "Spawn Lemon Tea for the player", Category = "Items"},
            new Effect("Send Black Tea", "give_142"){ Description = "Spawn Black Tea for the Player", Category = "Items"},
            new Effect("Send Peppermint", "give_143"){ Description = "Spawn Peppermint for the Player", Category = "Items"},
            new Effect("Send Mint", "give_144"){ Description = "Spawn Mint for the Player", Category = "Items"},
            new Effect("Send Valerian", "give_145"){ Description = "Spawn Valerian for the player", Category = "Items"},
        };
    }
}
