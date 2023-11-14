[assembly: System.Runtime.InteropServices.Guid("039b4c68-60fb-4b38-8f1f-e9e093618bc6")]

namespace WinderLogistics {
  public class ExternalPlugIn : Rhino.PlugIns.PlugIn {
    public ExternalPlugIn() {
      Instance = this;
    }

    public static ExternalPlugIn Instance {
      get;
      private set;
    }
  }

  public class ExternalCommand : Rhino.Commands.Command {
    public ExternalCommand() {
      Instance = this;
    }

    public static ExternalCommand Instance {
      get;
      private set;
    }

    public override System.String EnglishName {
      get {
        return "Winder";
      }
    }

    protected override Rhino.Commands.Result RunCommand(Rhino.RhinoDoc activeDocument, Rhino.Commands.RunMode runMode) {
      WinderWorkers.ProcessesExecutor processesExecutor = new WinderWorkers.ProcessesExecutor(true);

      processesExecutor.SetSeletedObjects();
      Rhino.RhinoApp.WriteLine("SETTED SELECTED OBJECTS");

      processesExecutor.FilterBoundaryObjects();
      Rhino.RhinoApp.WriteLine("FILTERED BOUNDARY OBJECTS");

      processesExecutor.PickBoundaryGeometries();
      Rhino.RhinoApp.WriteLine("PICKED BOUNDARY GEOMETRIES");

      processesExecutor.ProcessJoinedGeometries();
      Rhino.RhinoApp.WriteLine("PROCESSED JOINED GEOMETRIES");

      processesExecutor.CalculateBoundaryMathematics();
      Rhino.RhinoApp.WriteLine("CALCULATED BOUNDARY MATHEMATICS");

      processesExecutor.InitializeCorrelatedParameters();
      Rhino.RhinoApp.WriteLine("INITIALIZED CORRELATED PARAMETERS");

      processesExecutor.CalculateFlipPunctuations();
      Rhino.RhinoApp.WriteLine("CALCULATED FLIP PUNCTUATIONS");

      processesExecutor.HarmonizeBoundaryNormals();
      Rhino.RhinoApp.WriteLine("HARMONIZED BOUNDARY NORMALS");

      Rhino.RhinoDoc.ActiveDoc.Views.Redraw();

      return Rhino.Commands.Result.Success;
    }
  }
}