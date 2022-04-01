using Stateless;
using Stateless.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StateMachine
{
    public enum MachineMode
    {
        OffLine = 0,
        OnLine = 1
    }

    public enum MachineState
    {
        PowerOn = 0,
        Idle = 1,
        ServerOpen = 2,
        ServerOpenFailed = 3,
        Connected = 4,
        Disconnected = 5,
        Initializing = 6,
        InitializeFailed = 7,
        Ready = 8,
        Starting = 9,
        StartFailed = 10,
        Running = 11,
        Pausing = 12,
        PauseFailed = 13,
        Paused = 14,
        Resuming = 15,
        ResumeFailed = 16,
        Purging = 17,
        PurgeFailed = 18,
        Ending = 19,
        EndFailed = 20,
        Resetting = 21,
        ResetFailed = 22,
        Error = 23,
        Abort = 24
    }

    public enum MachineTrigger
    {
        OpenServer = 0,
        Connect = 1,
        Init = 2,
        Start = 3,
        Pause = 4,
        Resume = 5,
        End = 6,
        Purge = 7,
        Reset = 8,
        Abort = 9,
        Warn = 10,
        Error = 11
    }

    public enum MachineErrorLevel
    {
        Debug = 0,
        Info = 1,
        Warn = 2,
        Error = 3,
        Fatal = 4
    }

    public static class ErrorCode
    {
        public const uint EC_OK = 0;
        public const uint EC_SystemConfigFail = 20000001;
        public const uint EC_SystemAbortFail = 20000002;
        public const uint EC_SystemResetFail = 20000003;
        public const uint EC_SystemPurgeFail = 20000004;
        public const uint EC_SystemEndFail = 20000005;
        public const uint EC_SystemResumeFail = 20000006;
        public const uint EC_SystemPauseFail = 20000007;
        public const uint EC_SystemStartFail = 20000008;
        public const uint EC_SystemInitFail = 20000009;
        public const uint EC_SystemLoadFail = 20000010;
        public const uint EC_Exception = 20000011;
    }

    internal static class MachineEngine
    {
        public static StateMachine<MachineMode, MachineTrigger> StateMode = new StateMachine<MachineMode, MachineTrigger>(MachineMode.OffLine);
        public static StateMachine<MachineState, MachineTrigger> StateMachine = new StateMachine<MachineState, MachineTrigger>(MachineState.PowerOn);

        /// <summary>
        /// Machine system current run mode
        /// </summary>
        public static MachineMode CurrentMode { get; set; }

        /// <summary>
        /// Machine system current state
        /// </summary>
        public static MachineState CurrentState { get; set; }

        public static uint StateCode { get; set; }
        public static string StateMessage { get; set; }

        //public static StateMachine<MachineState, MachineTrigger>.TriggerWithParameters<bool> openTrigger = StateMachine.SetTriggerParameters<bool>(MachineTrigger.OpenServer);

        public static (uint, string) Init()
        {
            var retCode = ErrorCode.EC_OK;
            var retMsg = "";

            try
            {
                //var warnTrigger =
                //    StateMachine.SetTriggerParameters<string, uint, string, MachineErrorLevel>(MachineTrigger.Warn);
                //var errorTrigger =
                //    StateMachine.SetTriggerParameters<string, uint, string, MachineErrorLevel>(MachineTrigger.Error);

                StateMachine.OnTransitionCompleted((t) => Console.WriteLine($"{t.Source} -> {t.Destination} via {t.Trigger}"));

                #region PowerOn state transition

                StateMachine.Configure(MachineState.PowerOn)
                    .PermitIf(MachineTrigger.OpenServer, MachineState.ServerOpen, () => StateCode == ErrorCode.EC_OK)
                    .PermitIf(MachineTrigger.OpenServer, MachineState.ServerOpenFailed, () => StateCode != ErrorCode.EC_OK);

                StateMachine.Configure(MachineState.ServerOpen)
                    .Permit(MachineTrigger.Connect, MachineState.Idle);  // judge whether all clients connect to server, then trigger Connect

                StateMachine.Configure(MachineState.ServerOpenFailed);
                #endregion

                #region Click init button
                StateMachine.Configure(MachineState.Idle)
                    .Permit(MachineTrigger.Abort, MachineState.Abort) // exception result in abort state
                    .PermitIf(MachineTrigger.Init, MachineState.InitializeFailed, () => StateCode != ErrorCode.EC_OK)
                    .PermitIf(MachineTrigger.Init, MachineState.Ready, () => StateCode == ErrorCode.EC_OK)
                    .PermitReentryIf(MachineTrigger.Reset, () => StateCode == ErrorCode.EC_OK)
                    .PermitIf(MachineTrigger.Reset, MachineState.ResetFailed, () => StateCode != ErrorCode.EC_OK);
                //.InternalTransition<string, uint, string, MachineErrorLevel>(errorTrigger,
                //(moduleName, errCode, errMsg, errLevel, t) =>
                //    MachineSystem.Error(moduleName, errCode, errMsg, errLevel));

                StateMachine.Configure(MachineState.InitializeFailed) // already means abort state
                    .SubstateOf(MachineState.Abort)
                    .PermitIf(MachineTrigger.Reset, MachineState.ResetFailed, () => StateCode != ErrorCode.EC_OK)
                    .PermitIf(MachineTrigger.Reset, MachineState.Idle, () => StateCode == ErrorCode.EC_OK);
                #endregion

                #region Click start button
                StateMachine.Configure(MachineState.Ready)
                    .Permit(MachineTrigger.Abort, MachineState.Abort) // exception result in abort state
                    .PermitIf(MachineTrigger.Start, MachineState.StartFailed, () => StateCode != ErrorCode.EC_OK)
                    .PermitIf(MachineTrigger.Start, MachineState.Running, () => StateCode == ErrorCode.EC_OK);

                StateMachine.Configure(MachineState.StartFailed) // already means abort state
                    .SubstateOf(MachineState.Abort)
                    .PermitIf(MachineTrigger.Reset, MachineState.ResetFailed, () => StateCode != ErrorCode.EC_OK)
                    .PermitIf(MachineTrigger.Reset, MachineState.Idle, () => StateCode == ErrorCode.EC_OK);
                #endregion

                #region Click pause/resume/purge/end button
                StateMachine.Configure(MachineState.Running)
                    .Permit(MachineTrigger.Abort, MachineState.Abort) // exception result in abort state
                    .PermitIf(MachineTrigger.Pause, MachineState.PauseFailed, () => StateCode != ErrorCode.EC_OK)
                    .PermitIf(MachineTrigger.Pause, MachineState.Paused, () => StateCode == ErrorCode.EC_OK)
                    .PermitIf(MachineTrigger.End, MachineState.EndFailed, () => StateCode != ErrorCode.EC_OK)
                    .PermitIf(MachineTrigger.End, MachineState.Idle, () => StateCode == ErrorCode.EC_OK);
                //.PermitIf(MachineTrigger.Purge, MachineState.PurgeFailed, () => StateCode != ErrorCode.EC_OK)
                //.PermitIf(MachineTrigger.Purge, MachineState.Idle, () => StateCode == ErrorCode.EC_OK);
                //.InternalTransition<string, uint, string, MachineErrorLevel>(warnTrigger,
                //    (moduleName, errCode, errMsg, errLevel, t) =>
                //        MachineSystem.Error(moduleName, errCode, errMsg, errLevel))
                //.InternalTransition<string, uint, string, MachineErrorLevel>(errorTrigger,
                //    (moduleName, errCode, errMsg, errLevel, t) =>
                //        MachineSystem.Error(moduleName, errCode, errMsg, errLevel));

                //StateMachine.Configure(MachineState.Pausing)
                //    .SubstateOf(MachineState.Running);

                //StateMachine.Configure(MachineState.Resuming)
                //    .SubstateOf(MachineState.Running);

                //StateMachine.Configure(MachineState.Purging)
                //    .SubstateOf(MachineState.Running);

                //StateMachine.Configure(MachineState.Ending)
                //    .SubstateOf(MachineState.Running);

                // Only after paused can purge 
                StateMachine.Configure(MachineState.Paused)
                    .SubstateOf(MachineState.Running)
                    .PermitIf(MachineTrigger.Resume, MachineState.ResumeFailed, () => StateCode != ErrorCode.EC_OK)
                    .PermitIf(MachineTrigger.Resume, MachineState.Running, () => StateCode == ErrorCode.EC_OK)
                    .PermitIf(MachineTrigger.Purge, MachineState.PurgeFailed, () => StateCode != ErrorCode.EC_OK)
                    .PermitIf(MachineTrigger.Purge, MachineState.Idle, () => StateCode == ErrorCode.EC_OK);

                StateMachine.Configure(MachineState.PauseFailed)    // already means abort state
                    .SubstateOf(MachineState.Abort)
                    .PermitIf(MachineTrigger.Reset, MachineState.ResetFailed, () => StateCode != ErrorCode.EC_OK)
                    .PermitIf(MachineTrigger.Reset, MachineState.Idle, () => StateCode == ErrorCode.EC_OK);

                StateMachine.Configure(MachineState.ResumeFailed)   // already means abort state
                    .SubstateOf(MachineState.Abort)
                    .PermitIf(MachineTrigger.Reset, MachineState.ResetFailed, () => StateCode != ErrorCode.EC_OK)
                    .PermitIf(MachineTrigger.Reset, MachineState.Idle, () => StateCode == ErrorCode.EC_OK);

                StateMachine.Configure(MachineState.PurgeFailed)    // already means abort state
                    .SubstateOf(MachineState.Abort)
                    .PermitIf(MachineTrigger.Reset, MachineState.ResetFailed, () => StateCode != ErrorCode.EC_OK)
                    .PermitIf(MachineTrigger.Reset, MachineState.Idle, () => StateCode == ErrorCode.EC_OK);

                StateMachine.Configure(MachineState.EndFailed)      // already means abort state
                    .SubstateOf(MachineState.Abort)
                    .PermitIf(MachineTrigger.Reset, MachineState.ResetFailed, () => StateCode != ErrorCode.EC_OK)
                    .PermitIf(MachineTrigger.Reset, MachineState.Idle, () => StateCode == ErrorCode.EC_OK);
                #endregion

                #region Click reset button
                //StateMachine.Configure(MachineState.Resetting)
                //    .Permit(MachineTrigger.Abort, MachineState.Abort) // exception result in abort state

                StateMachine.Configure(MachineState.ResetFailed)    // already means abort state
                    .SubstateOf(MachineState.Abort)
                    .PermitIf(MachineTrigger.Reset, MachineState.Abort, () => StateCode != ErrorCode.EC_OK)
                    .PermitIf(MachineTrigger.Reset, MachineState.Idle, () => StateCode == ErrorCode.EC_OK);

                StateMachine.Configure(MachineState.Abort)
                    .PermitReentry(MachineTrigger.Abort)
                    //.PermitReentryIf(MachineTrigger.Reset, () => StateCode != ErrorCode.EC_OK)                     // Abort → Abort when error occurs in resetting
                    .PermitIf(MachineTrigger.Reset, MachineState.ResetFailed, () => StateCode != ErrorCode.EC_OK)     // Abort → ResetFailed when error occurs in resetting
                    .PermitIf(MachineTrigger.Reset, MachineState.Idle, () => StateCode == ErrorCode.EC_OK);
                #endregion
            }
            catch (Exception ex)
            {
                retCode = ErrorCode.EC_Exception;
                retMsg = "Machine system engine init fail: " + ex.InnerException.Message;
            }
            return (retCode, retMsg);
        }

        public static string GetStateGraph() => UmlDotGraph.Format(StateMachine.GetInfo());
    }

    public class Program
    {
        static void Main(string[] args)
        {
            MachineEngine.Init();
            Console.WriteLine(MachineEngine.GetStateGraph());
            Console.ReadKey();
        }
    }
}
