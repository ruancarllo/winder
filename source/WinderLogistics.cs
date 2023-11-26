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
        Rhino.RhinoApp.WriteLine("Winder: Verified objects selection");

        Rhino.RhinoApp.RunScript("!_Join", false);
        Rhino.RhinoApp.RunScript("!_Explode", false);
        
        processesExecutor.DefineEssentialLayers();
        Rhino.RhinoApp.WriteLine("Winder: Defined essential layers");

        processesExecutor.DefineInteractiveAttributes();
        Rhino.RhinoApp.WriteLine("Winder: Defined interactive attributes");

        processesExecutor.RegisterExplodedSelectedObjects();
        Rhino.RhinoApp.WriteLine("Winder: Registered exploded selected objects");

        processesExecutor.RepaintExplodedSelectedObjects();
        Rhino.RhinoApp.WriteLine("Winder: Repainted exploded selected objects");

        processesExecutor.DeleteUnessentialLayers();
        Rhino.RhinoApp.WriteLine("Winder: Deleted unessential layers");

        processesExecutor.FilterExplodedBoundaryObjects();
        Rhino.RhinoApp.WriteLine("Winder: Filtered exploded boundary objects");

        processesExecutor.SetFragmentedBoundaries();
        Rhino.RhinoApp.WriteLine("Winder: Setted fragmented boundaries");

        processesExecutor.SetConnectedBoundaries();
        Rhino.RhinoApp.WriteLine("Winder: Setted connected boundaries");

        processesExecutor.DefineBoundaryCollectionAttributes();
        Rhino.RhinoApp.WriteLine("Winder: Defined boundary collection attributes");

        processesExecutor.FindBoundaryIntegrationMidpoints();
        Rhino.RhinoApp.WriteLine("Winder: Found boundary integration midpoints");

        processesExecutor.HarmonizeExplodedBoundaryObjectsNormals();
        Rhino.RhinoApp.WriteLine("Winder: Harmonized exploded boundary objects normals");

        processesExecutor.DeleteInteractiveLayer();
        Rhino.RhinoApp.WriteLine("Winder: Deleted interactive layer");

        return Rhino.Commands.Result.Success;
      }

      catch (System.Exception exception) {
        Rhino.RhinoApp.WriteLine(exception.Message);

        return Rhino.Commands.Result.Failure;
      }
    }
  }
}