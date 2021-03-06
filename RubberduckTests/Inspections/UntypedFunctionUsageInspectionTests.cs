using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Vbe.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Rubberduck.Inspections;
using Rubberduck.Parsing;
using Rubberduck.Parsing.Annotations;
using Rubberduck.Parsing.Symbols;
using Rubberduck.Parsing.VBA;
using Rubberduck.VBEditor;
using Rubberduck.VBEditor.Extensions;
using Rubberduck.VBEditor.VBEHost;
using RubberduckTests.Mocks;

namespace RubberduckTests.Inspections
{
    [TestClass]
    public class UntypedFunctionUsageInspectionTests
    {
        [TestMethod]
        [TestCategory("Inspections")]
        public void UntypedFunctionUsage_ReturnsResult()
        {
            const string inputCode =
@"Sub Foo()
    Dim str As String
    str = Left(""test"", 1)
End Sub";

            //Arrange
            var builder = new MockVbeBuilder();
            var project = builder.ProjectBuilder("VBAProject", vbext_ProjectProtection.vbext_pp_none)
                .AddComponent("MyClass", vbext_ComponentType.vbext_ct_ClassModule, inputCode)
                .AddReference("VBA", "C:\\Program Files\\Common Files\\Microsoft Shared\\VBA\\VBA7.1\\VBE7.DLL", true)
                .Build();
            var vbe = builder.AddProject(project).Build();

            var mockHost = new Mock<IHostApplication>();
            mockHost.SetupAllProperties();
            var parser = MockParser.Create(vbe.Object, new RubberduckParserState(new Mock<ISinks>().Object));

            GetBuiltInDeclarations().ForEach(d => parser.State.AddDeclaration(d));

            parser.Parse(new CancellationTokenSource());
            if (parser.State.Status >= ParserState.Error) { Assert.Inconclusive("Parser Error"); }

            var inspection = new UntypedFunctionUsageInspection(parser.State);
            var inspectionResults = inspection.GetInspectionResults();

            Assert.AreEqual(1, inspectionResults.Count());
        }

        [TestMethod]
        [TestCategory("Inspections")]
        public void UntypedFunctionUsage_DoesNotReturnResult()
        {
            const string inputCode =
@"Sub Foo()
    Dim str As String
    str = Left$(""test"", 1)
End Sub";

            //Arrange
            var builder = new MockVbeBuilder();
            var project = builder.ProjectBuilder("VBAProject", vbext_ProjectProtection.vbext_pp_none)
                .AddComponent("MyClass", vbext_ComponentType.vbext_ct_ClassModule, inputCode)
                .Build();
            var vbe = builder.AddProject(project).Build();

            var mockHost = new Mock<IHostApplication>();
            mockHost.SetupAllProperties();
            var parser = MockParser.Create(vbe.Object, new RubberduckParserState(new Mock<ISinks>().Object));

            GetBuiltInDeclarations().ForEach(d => parser.State.AddDeclaration(d));

            parser.Parse(new CancellationTokenSource());
            if (parser.State.Status >= ParserState.Error) { Assert.Inconclusive("Parser Error"); }

            var inspection = new UntypedFunctionUsageInspection(parser.State);
            var inspectionResults = inspection.GetInspectionResults();

            Assert.IsFalse(inspectionResults.Any());
        }

        [TestMethod]
        [TestCategory("Inspections")]
        public void UntypedFunctionUsage_Ignored_DoesNotReturnResult()
        {
            const string inputCode =
@"Sub Foo()
    Dim str As String

    '@Ignore UntypedFunctionUsage
    str = Left(""test"", 1)
End Sub";

            //Arrange
            var builder = new MockVbeBuilder();
            var project = builder.ProjectBuilder("VBAProject", vbext_ProjectProtection.vbext_pp_none)
                .AddComponent("MyClass", vbext_ComponentType.vbext_ct_ClassModule, inputCode)
                .AddReference("VBA", "C:\\Program Files\\Common Files\\Microsoft Shared\\VBA\\VBA7.1\\VBE7.DLL", true)
                .Build();
            var vbe = builder.AddProject(project).Build();

            var mockHost = new Mock<IHostApplication>();
            mockHost.SetupAllProperties();
            var parser = MockParser.Create(vbe.Object, new RubberduckParserState(new Mock<ISinks>().Object));

            GetBuiltInDeclarations().ForEach(d => parser.State.AddDeclaration(d));

            parser.Parse(new CancellationTokenSource());
            if (parser.State.Status >= ParserState.Error) { Assert.Inconclusive("Parser Error"); }

            var inspection = new UntypedFunctionUsageInspection(parser.State);
            var inspectionResults = inspection.GetInspectionResults();

            Assert.IsFalse(inspectionResults.Any());
        }

        [TestMethod]
        [TestCategory("Inspections")]
        public void UntypedFunctionUsage_QuickFixWorks()
        {
            const string inputCode =
@"Sub Foo()
    Dim str As String
    str = Left(""test"", 1)
End Sub";

            const string expectedCode =
@"Sub Foo()
    Dim str As String
    str = Left$(""test"", 1)
End Sub";

            //Arrange
            var builder = new MockVbeBuilder();
            var project = builder.ProjectBuilder("VBAProject", vbext_ProjectProtection.vbext_pp_none)
                .AddComponent("MyClass", vbext_ComponentType.vbext_ct_ClassModule, inputCode)
                .AddReference("VBA", "C:\\Program Files\\Common Files\\Microsoft Shared\\VBA\\VBA7.1\\VBE7.DLL", true)
                .Build();
            var vbe = builder.AddProject(project).Build();

            var module = project.Object.VBComponents.Item(0).CodeModule;
            var mockHost = new Mock<IHostApplication>();
            mockHost.SetupAllProperties();
            var parser = MockParser.Create(vbe.Object, new RubberduckParserState(new Mock<ISinks>().Object));

            GetBuiltInDeclarations().ForEach(d => parser.State.AddDeclaration(d));

            parser.Parse(new CancellationTokenSource());
            if (parser.State.Status >= ParserState.Error) { Assert.Inconclusive("Parser Error"); }

            var inspection = new UntypedFunctionUsageInspection(parser.State);
            var inspectionResults = inspection.GetInspectionResults();

            inspectionResults.First().QuickFixes.First().Fix();
            
            Assert.AreEqual(expectedCode, module.Lines());
        }

        [TestMethod]
        [TestCategory("Inspections")]
        public void UntypedFunctionUsage_IgnoreQuickFixWorks()
        {
            const string inputCode =
@"Sub Foo()
    Dim str As String
    str = Left(""test"", 1)
End Sub";

            const string expectedCode =
@"Sub Foo()
    Dim str As String
'@Ignore UntypedFunctionUsage
    str = Left(""test"", 1)
End Sub";

            //Arrange
            var builder = new MockVbeBuilder();
            var project = builder.ProjectBuilder("VBAProject", vbext_ProjectProtection.vbext_pp_none)
                .AddComponent("MyClass", vbext_ComponentType.vbext_ct_ClassModule, inputCode)
                .AddReference("VBA", "C:\\Program Files\\Common Files\\Microsoft Shared\\VBA\\VBA7.1\\VBE7.DLL", true)
                .Build();
            var vbe = builder.AddProject(project).Build();

            var module = project.Object.VBComponents.Item(0).CodeModule;
            var mockHost = new Mock<IHostApplication>();
            mockHost.SetupAllProperties();
            var parser = MockParser.Create(vbe.Object, new RubberduckParserState(new Mock<ISinks>().Object));

            GetBuiltInDeclarations().ForEach(d => parser.State.AddDeclaration(d));

            parser.Parse(new CancellationTokenSource());
            if (parser.State.Status >= ParserState.Error) { Assert.Inconclusive("Parser Error"); }

            var inspection = new UntypedFunctionUsageInspection(parser.State);
            var inspectionResults = inspection.GetInspectionResults();

            inspectionResults.First().QuickFixes.Single(s => s is IgnoreOnceQuickFix).Fix();

            Assert.AreEqual(expectedCode, module.Lines());
        }

        [TestMethod]
        [TestCategory("Inspections")]
        public void InspectionType()
        {
            var inspection = new UntypedFunctionUsageInspection(null);
            Assert.AreEqual(CodeInspectionType.LanguageOpportunities, inspection.InspectionType);
        }

        [TestMethod]
        [TestCategory("Inspections")]
        public void InspectionName()
        {
            const string inspectionName = "UntypedFunctionUsageInspection";
            var inspection = new UntypedFunctionUsageInspection(null);

            Assert.AreEqual(inspectionName, inspection.Name);
        }

        private List<Declaration> GetBuiltInDeclarations()
        {
            var vbaDeclaration = new ProjectDeclaration(
                new QualifiedMemberName(new QualifiedModuleName("VBA", "C:\\Program Files\\Common Files\\Microsoft Shared\\VBA\\VBA7.1\\VBE7.DLL", "VBA"), "VBA"),
                "VBA",
                true);

            var conversionModule = new ProceduralModuleDeclaration(
                new QualifiedMemberName(new QualifiedModuleName("VBA", "C:\\Program Files\\Common Files\\Microsoft Shared\\VBA\\VBA7.1\\VBE7.DLL", "Conversion"), "Conversion"),
                vbaDeclaration,
                "Conversion",
                true,
                new List<IAnnotation>(),
                new Attributes());

            var fileSystemModule = new ProceduralModuleDeclaration(
                new QualifiedMemberName(new QualifiedModuleName("VBA", "C:\\Program Files\\Common Files\\Microsoft Shared\\VBA\\VBA7.1\\VBE7.DLL", "FileSystem"), "FileSystem"),
                vbaDeclaration,
                "FileSystem",
                true,
                new List<IAnnotation>(),
                new Attributes());

            var interactionModule = new ProceduralModuleDeclaration(
                new QualifiedMemberName(new QualifiedModuleName("VBA", "C:\\Program Files\\Common Files\\Microsoft Shared\\VBA\\VBA7.1\\VBE7.DLL", "Interaction"), "Interaction"),
                vbaDeclaration,
                "Interaction",
                true,
                new List<IAnnotation>(),
                new Attributes());

            var stringsModule = new ProceduralModuleDeclaration(
                new QualifiedMemberName(new QualifiedModuleName("VBA", "C:\\Program Files\\Common Files\\Microsoft Shared\\VBA\\VBA7.1\\VBE7.DLL", "Strings"), "Strings"),
                vbaDeclaration,
                "Strings",
                true,
                new List<IAnnotation>(),
                new Attributes());

            var commandFunction = new FunctionDeclaration(
                new QualifiedMemberName(new QualifiedModuleName("VBA", "C:\\Program Files\\Common Files\\Microsoft Shared\\VBA\\VBA7.1\\VBE7.DLL", "Interaction"), "_B_var_Command"),
                interactionModule,
                interactionModule,
                "Variant",
                null,
                null,
                Accessibility.Global,
                null,
                Selection.Home,
                false,
                true,
                new List<IAnnotation>(),
                new Attributes());

            var environFunction = new FunctionDeclaration(
                new QualifiedMemberName(new QualifiedModuleName("VBA", "C:\\Program Files\\Common Files\\Microsoft Shared\\VBA\\VBA7.1\\VBE7.DLL", "Interaction"), "_B_var_Environ"),
                interactionModule,
                interactionModule,
                "Variant",
                null,
                null,
                Accessibility.Global,
                null,
                Selection.Home,
                false,
                true,
                new List<IAnnotation>(),
                new Attributes());

            var rtrimFunction = new FunctionDeclaration(
                new QualifiedMemberName(new QualifiedModuleName("VBA", "C:\\Program Files\\Common Files\\Microsoft Shared\\VBA\\VBA7.1\\VBE7.DLL", "Strings"), "_B_var_RTrim"),
                stringsModule,
                stringsModule,
                "Variant",
                null,
                null,
                Accessibility.Global,
                null,
                Selection.Home,
                false,
                true,
                new List<IAnnotation>(),
                new Attributes());

            var chrFunction = new FunctionDeclaration(
                new QualifiedMemberName(new QualifiedModuleName("VBA", "C:\\Program Files\\Common Files\\Microsoft Shared\\VBA\\VBA7.1\\VBE7.DLL", "Strings"), "_B_var_Chr"),
                stringsModule,
                stringsModule,
                "Variant",
                null,
                null,
                Accessibility.Global,
                null,
                Selection.Home,
                false,
                true,
                new List<IAnnotation>(),
                new Attributes());

            var formatFunction = new FunctionDeclaration(
                new QualifiedMemberName(new QualifiedModuleName("VBA", "C:\\Program Files\\Common Files\\Microsoft Shared\\VBA\\VBA7.1\\VBE7.DLL", "Strings"), "_B_var_Format"),
                stringsModule,
                stringsModule,
                "Variant",
                null,
                null,
                Accessibility.Global,
                null,
                Selection.Home,
                false,
                true,
                new List<IAnnotation>(),
                new Attributes());

            var firstFormatParam = new ParameterDeclaration(
                new QualifiedMemberName(new QualifiedModuleName("VBA", "C:\\Program Files\\Common Files\\Microsoft Shared\\VBA\\VBA7.1\\VBE7.DLL", "Strings"), "Expression"),
                formatFunction,
                "Variant",
                null,
                null,
                false,
                true);

            var secondFormatParam = new ParameterDeclaration(
                new QualifiedMemberName(new QualifiedModuleName("VBA", "C:\\Program Files\\Common Files\\Microsoft Shared\\VBA\\VBA7.1\\VBE7.DLL", "Strings"), "Format"),
                formatFunction,
                "Variant",
                null,
                null,
                true,
                true);

            var thirdFormatParam = new ParameterDeclaration(
                new QualifiedMemberName(new QualifiedModuleName("VBA", "C:\\Program Files\\Common Files\\Microsoft Shared\\VBA\\VBA7.1\\VBE7.DLL", "Strings"), "FirstDayOfWeek"),
                formatFunction,
                "VbDayOfWeek",
                null,
                null,
                true,
                false);

            formatFunction.AddParameter(firstFormatParam);
            formatFunction.AddParameter(secondFormatParam);
            formatFunction.AddParameter(thirdFormatParam);

            var rightFunction = new FunctionDeclaration(
                new QualifiedMemberName(new QualifiedModuleName("VBA", "C:\\Program Files\\Common Files\\Microsoft Shared\\VBA\\VBA7.1\\VBE7.DLL", "Strings"), "_B_var_Right"),
                stringsModule,
                stringsModule,
                "Variant",
                null,
                null,
                Accessibility.Global,
                null,
                Selection.Home,
                false,
                true,
                new List<IAnnotation>(),
                new Attributes());

            var firstRightParam = new ParameterDeclaration(
                new QualifiedMemberName(new QualifiedModuleName("VBA", "C:\\Program Files\\Common Files\\Microsoft Shared\\VBA\\VBA7.1\\VBE7.DLL", "Strings"), "String"),
                rightFunction,
                "Variant",
                null,
                null,
                false,
                true);

            rightFunction.AddParameter(firstRightParam);

            var lcaseFunction = new FunctionDeclaration(
                new QualifiedMemberName(new QualifiedModuleName("VBA", "C:\\Program Files\\Common Files\\Microsoft Shared\\VBA\\VBA7.1\\VBE7.DLL", "Strings"), "_B_var_LCase"),
                stringsModule,
                stringsModule,
                "Variant",
                null,
                null,
                Accessibility.Global,
                null,
                Selection.Home,
                false,
                true,
                new List<IAnnotation>(),
                new Attributes());

            var leftbFunction = new FunctionDeclaration(
                new QualifiedMemberName(new QualifiedModuleName("VBA", "C:\\Program Files\\Common Files\\Microsoft Shared\\VBA\\VBA7.1\\VBE7.DLL", "Strings"), "_B_var_LeftB"),
                stringsModule,
                stringsModule,
                "Variant",
                null,
                null,
                Accessibility.Global,
                null,
                Selection.Home,
                false,
                true,
                new List<IAnnotation>(),
                new Attributes());

            var firstLeftBParam = new ParameterDeclaration(
                new QualifiedMemberName(new QualifiedModuleName("VBA", "C:\\Program Files\\Common Files\\Microsoft Shared\\VBA\\VBA7.1\\VBE7.DLL", "Strings"), "String"),
                leftbFunction,
                "Variant",
                null,
                null,
                false,
                true);

            leftbFunction.AddParameter(firstLeftBParam);

            var chrwFunction = new FunctionDeclaration(
                new QualifiedMemberName(new QualifiedModuleName("VBA", "C:\\Program Files\\Common Files\\Microsoft Shared\\VBA\\VBA7.1\\VBE7.DLL", "Strings"), "_B_var_ChrW"),
                stringsModule,
                stringsModule,
                "Variant",
                null,
                null,
                Accessibility.Global,
                null,
                Selection.Home,
                false,
                true,
                new List<IAnnotation>(),
                new Attributes());

            var leftFunction = new FunctionDeclaration(
                new QualifiedMemberName(new QualifiedModuleName("VBA", "C:\\Program Files\\Common Files\\Microsoft Shared\\VBA\\VBA7.1\\VBE7.DLL", "Strings"), "_B_var_Left"),
                stringsModule,
                stringsModule,
                "Variant",
                null,
                null,
                Accessibility.Global,
                null,
                Selection.Home,
                false,
                true,
                new List<IAnnotation>(),
                new Attributes());

            var firstLeftParam = new ParameterDeclaration(
                new QualifiedMemberName(new QualifiedModuleName("VBA", "C:\\Program Files\\Common Files\\Microsoft Shared\\VBA\\VBA7.1\\VBE7.DLL", "Strings"), "String"),
                leftFunction,
                "Variant",
                null,
                null,
                false,
                true);

            leftFunction.AddParameter(firstLeftParam);

            var rightbFunction = new FunctionDeclaration(
                new QualifiedMemberName(new QualifiedModuleName("VBA", "C:\\Program Files\\Common Files\\Microsoft Shared\\VBA\\VBA7.1\\VBE7.DLL", "Strings"), "_B_var_RightB"),
                stringsModule,
                stringsModule,
                "Variant",
                null,
                null,
                Accessibility.Global,
                null,
                Selection.Home,
                false,
                true,
                new List<IAnnotation>(),
                new Attributes());

            var firstRightBParam = new ParameterDeclaration(
                new QualifiedMemberName(new QualifiedModuleName("VBA", "C:\\Program Files\\Common Files\\Microsoft Shared\\VBA\\VBA7.1\\VBE7.DLL", "Strings"), "String"),
                rightbFunction,
                "Variant",
                null,
                null,
                false,
                true);

            rightbFunction.AddParameter(firstRightBParam);

            var midbFunction = new FunctionDeclaration(
                new QualifiedMemberName(new QualifiedModuleName("VBA", "C:\\Program Files\\Common Files\\Microsoft Shared\\VBA\\VBA7.1\\VBE7.DLL", "Strings"), "_B_var_MidB"),
                stringsModule,
                stringsModule,
                "Variant",
                null,
                null,
                Accessibility.Global,
                null,
                Selection.Home,
                false,
                true,
                new List<IAnnotation>(),
                new Attributes());

            var firstMidBParam = new ParameterDeclaration(
                new QualifiedMemberName(new QualifiedModuleName("VBA", "C:\\Program Files\\Common Files\\Microsoft Shared\\VBA\\VBA7.1\\VBE7.DLL", "Strings"), "String"),
                midbFunction,
                "Variant",
                null,
                null,
                false,
                true);

            var secondMidBParam = new ParameterDeclaration(
                new QualifiedMemberName(new QualifiedModuleName("VBA", "C:\\Program Files\\Common Files\\Microsoft Shared\\VBA\\VBA7.1\\VBE7.DLL", "Strings"), "Start"),
                midbFunction,
                "Long",
                null,
                null,
                false,
                false);

            midbFunction.AddParameter(firstMidBParam);
            midbFunction.AddParameter(secondMidBParam);

            var ucaseFunction = new FunctionDeclaration(
                new QualifiedMemberName(new QualifiedModuleName("VBA", "C:\\Program Files\\Common Files\\Microsoft Shared\\VBA\\VBA7.1\\VBE7.DLL", "Strings"), "_B_var_UCase"),
                stringsModule,
                stringsModule,
                "Variant",
                null,
                null,
                Accessibility.Global,
                null,
                Selection.Home,
                false,
                true,
                new List<IAnnotation>(),
                new Attributes());

            var trimFunction = new FunctionDeclaration(
                new QualifiedMemberName(new QualifiedModuleName("VBA", "C:\\Program Files\\Common Files\\Microsoft Shared\\VBA\\VBA7.1\\VBE7.DLL", "Strings"), "_B_var_Trim"),
                stringsModule,
                stringsModule,
                "Variant",
                null,
                null,
                Accessibility.Global,
                null,
                Selection.Home,
                false,
                true,
                new List<IAnnotation>(),
                new Attributes());

            var ltrimFunction = new FunctionDeclaration(
                new QualifiedMemberName(new QualifiedModuleName("VBA", "C:\\Program Files\\Common Files\\Microsoft Shared\\VBA\\VBA7.1\\VBE7.DLL", "Strings"), "_B_var_LTrim"),
                stringsModule,
                stringsModule,
                "Variant",
                null,
                null,
                Accessibility.Global,
                null,
                Selection.Home,
                false,
                true,
                new List<IAnnotation>(),
                new Attributes());

            var midFunction = new FunctionDeclaration(
                new QualifiedMemberName(new QualifiedModuleName("VBA", "C:\\Program Files\\Common Files\\Microsoft Shared\\VBA\\VBA7.1\\VBE7.DLL", "Strings"), "_B_var_Mid"),
                stringsModule,
                stringsModule,
                "Variant",
                null,
                null,
                Accessibility.Global,
                null,
                Selection.Home,
                false,
                true,
                new List<IAnnotation>(),
                new Attributes());

            var firstMidParam = new ParameterDeclaration(
                new QualifiedMemberName(new QualifiedModuleName("VBA", "C:\\Program Files\\Common Files\\Microsoft Shared\\VBA\\VBA7.1\\VBE7.DLL", "Strings"), "String"),
                midbFunction,
                "Variant",
                null,
                null,
                false,
                true);

            var secondMidParam = new ParameterDeclaration(
                new QualifiedMemberName(new QualifiedModuleName("VBA", "C:\\Program Files\\Common Files\\Microsoft Shared\\VBA\\VBA7.1\\VBE7.DLL", "Strings"), "Start"),
                midbFunction,
                "Long",
                null,
                null,
                false,
                false);

            midFunction.AddParameter(firstMidParam);
            midFunction.AddParameter(secondMidParam);

            var hexFunction = new FunctionDeclaration(
                new QualifiedMemberName(new QualifiedModuleName("VBA", "C:\\Program Files\\Common Files\\Microsoft Shared\\VBA\\VBA7.1\\VBE7.DLL", "Strings"), "_B_var_Hex"),
                conversionModule,
                conversionModule,
                "Variant",
                null,
                null,
                Accessibility.Global,
                null,
                Selection.Home,
                false,
                true,
                new List<IAnnotation>(),
                new Attributes());

            var octFunction = new FunctionDeclaration(
                new QualifiedMemberName(new QualifiedModuleName("VBA", "C:\\Program Files\\Common Files\\Microsoft Shared\\VBA\\VBA7.1\\VBE7.DLL", "Strings"), "_B_var_Oct"),
                conversionModule,
                conversionModule,
                "Variant",
                null,
                null,
                Accessibility.Global,
                null,
                Selection.Home,
                false,
                true,
                new List<IAnnotation>(),
                new Attributes());

            var errorFunction = new FunctionDeclaration(
                new QualifiedMemberName(new QualifiedModuleName("VBA", "C:\\Program Files\\Common Files\\Microsoft Shared\\VBA\\VBA7.1\\VBE7.DLL", "Strings"), "_B_var_Error"),
                conversionModule,
                conversionModule,
                "Variant",
                null,
                null,
                Accessibility.Global,
                null,
                Selection.Home,
                false,
                true,
                new List<IAnnotation>(),
                new Attributes());

            var strFunction = new FunctionDeclaration(
                new QualifiedMemberName(new QualifiedModuleName("VBA", "C:\\Program Files\\Common Files\\Microsoft Shared\\VBA\\VBA7.1\\VBE7.DLL", "Strings"), "_B_var_Str"),
                conversionModule,
                conversionModule,
                "Variant",
                null,
                null,
                Accessibility.Global,
                null,
                Selection.Home,
                false,
                true,
                new List<IAnnotation>(),
                new Attributes());

            var curDirFunction = new FunctionDeclaration(
                new QualifiedMemberName(new QualifiedModuleName("VBA", "C:\\Program Files\\Common Files\\Microsoft Shared\\VBA\\VBA7.1\\VBE7.DLL", "Strings"), "_B_var_CurDir"),
                fileSystemModule,
                fileSystemModule,
                "Variant",
                null,
                null,
                Accessibility.Global,
                null,
                Selection.Home,
                false,
                true,
                new List<IAnnotation>(),
                new Attributes());

            return new List<Declaration>
            {
                vbaDeclaration,
                conversionModule,
                fileSystemModule,
                interactionModule,
                stringsModule,
                commandFunction,
                environFunction,
                rtrimFunction,
                chrFunction,
                formatFunction,
                firstFormatParam,
                secondFormatParam,
                thirdFormatParam,
                rightFunction,
                firstRightParam,
                lcaseFunction,
                leftbFunction,
                firstLeftBParam,
                chrwFunction,
                leftFunction,
                firstLeftParam,
                rightbFunction,
                firstRightBParam,
                midbFunction,
                firstMidBParam,
                secondMidBParam,
                ucaseFunction,
                trimFunction,
                ltrimFunction,
                midFunction,
                firstMidParam,
                secondMidParam,
                hexFunction,
                octFunction,
                errorFunction,
                strFunction,
                curDirFunction
            };
        }
    }
}
