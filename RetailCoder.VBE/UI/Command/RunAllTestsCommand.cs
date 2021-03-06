using System;
using System.Diagnostics;
using Microsoft.Vbe.Interop;
using NLog;
using Rubberduck.Parsing.VBA;
using Rubberduck.UI.UnitTesting;
using Rubberduck.UnitTesting;
using Rubberduck.VBEditor.Extensions;
using System.Linq;

namespace Rubberduck.UI.Command
{
    /// <summary>
    /// A command that runs all Rubberduck unit tests in the VBE.
    /// </summary>
    public class RunAllTestsCommand : CommandBase
    {
        private readonly VBE _vbe;
        private readonly ITestEngine _engine;
        private readonly TestExplorerModel _model;
        private readonly RubberduckParserState _state;
        
        public RunAllTestsCommand(VBE vbe, RubberduckParserState state, ITestEngine engine, TestExplorerModel model) : base(LogManager.GetCurrentClassLogger())
        {
            _vbe = vbe;
            _engine = engine;
            _model = model;
            _state = state;
        }

        private static readonly ParserState[] AllowedRunStates = { ParserState.ResolvedDeclarations, ParserState.ResolvingReferences, ParserState.Ready };

        protected override bool CanExecuteImpl(object parameter)
        {
            return _vbe.IsInDesignMode() && AllowedRunStates.Contains(_state.Status);
        }

        protected override void ExecuteImpl(object parameter)
        {
            if (!_state.IsDirty())
            {
                RunTests();
            }
            else
            {
                _model.TestsRefreshed += TestsRefreshed;
                _model.Refresh();
            }
        }

        private void TestsRefreshed(object sender, EventArgs e)
        {
            RunTests();
        }

        private void RunTests()
        {
            _model.TestsRefreshed -= TestsRefreshed;

            var stopwatch = new Stopwatch();

            _model.ClearLastRun();
            _model.IsBusy = true;

            stopwatch.Start();
            _engine.Run(_model.Tests);
            stopwatch.Stop();

            _model.IsBusy = false;

            OnRunCompleted(new TestRunEventArgs(stopwatch.ElapsedMilliseconds));
        }

        public event EventHandler<TestRunEventArgs> RunCompleted;
        protected virtual void OnRunCompleted(TestRunEventArgs e)
        {
            var handler = RunCompleted;
            if (handler != null)
            {
                handler.Invoke(this, e);
            }
        }
    }
    
    public class TestRunEventArgs : EventArgs
    {
        public long Duration { get; private set; }

        public TestRunEventArgs(long duration)
        {
            Duration = duration;
        }
    }
}
