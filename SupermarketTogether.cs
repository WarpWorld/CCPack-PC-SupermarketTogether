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
            new Effect("Turn Lights On", "lighton") { Description = "Toggle Lights On", Category = "Store"},
            
            
            new Effect("Give $100", "money100") { Description = "Gives 100 of the Selected Currency", Category = "Money"},

            new Effect("Give $1000", "money1000"){ Description = "Gives 1000 of the Selected Currency", Category = "Money"},

            new Effect("Give 1 Franchise Point", "give1fp"){ Description = "Gives 1 Franchise Point to the Player(s)", Category = "Stats"},
        };
    }
}
