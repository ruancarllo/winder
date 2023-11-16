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
      WinderWorkers.ProcessesExecutor processesExecutor = new WinderWorkers.ProcessesExecutor(true, true, true);

      processesExecutor.SetSeletedObjects();
      processesExecutor.SetBoundaryObjects();
      processesExecutor.SetBoundaryGeometries();
      processesExecutor.SetJoinedGeometries();
      processesExecutor.SetBoundaryMathematics();
      processesExecutor.HarmonizeBoundaryNormals();

      Rhino.RhinoDoc.ActiveDoc.Views.Redraw();

      return Rhino.Commands.Result.Success;
    }
  }
}