using LabFusion.Core.Gamemodes;

namespace LabFusion.MarrowIntegration
{
    [MelonLoader.RegisterTypeInIl2Cpp]
    public class CTFProxy : FusionMarrowBehaviour
    {
        public CTFProxy(System.IntPtr ptr) : base(ptr) { }

        /// <summary>
        /// Registers the flag with a team. Can only be done once per flag!
        /// </summary>
        /// <param name="TeamName"></param>
        public void RegisterFlag(string TeamName)
        {
            if (!CaptureTheFlag.Instance.RegisterFlag(TeamName))
            {
                // Already registered for that team!
                return;
            }
        }
    }
}