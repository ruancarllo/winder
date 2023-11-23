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

  [Rhino.Commands.CommandStyle(Rhino.Commands.Style.ScriptRunner)]
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
      WinderHandlers.ProcessesExecutor processesExecutor = new WinderHandlers.ProcessesExecutor();

      try {
        processesExecutor.VerifyObjectsSelection();

        Rhino.RhinoApp.RunScript("!_Join", false);
        Rhino.RhinoApp.RunScript("!_Explode", false);
        
        processesExecutor.DefineEssentialLayers();
        processesExecutor.DefineInteractiveAttributes();
        processesExecutor.RegisterExplodedSelectedObjects();
        processesExecutor.RepaintExplodedSelectedObjects();
        processesExecutor.DeleteUnessentialLayers();
        processesExecutor.FilterExplodedBoundaryObjects();
        processesExecutor.SetFragmentedBoundaries();
        processesExecutor.SetConnectedBoundaries();
        processesExecutor.DefineBoundaryCollectionAttributes();
        processesExecutor.FindBoundaryIntegrationMidpoints();

        return Rhino.Commands.Result.Success;
      }

      catch (System.Exception exception) {
        Rhino.RhinoApp.WriteLine(exception.Message);

        return Rhino.Commands.Result.Failure;
      }
    }
  }
}