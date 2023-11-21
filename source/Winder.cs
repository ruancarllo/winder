[assembly: System.Runtime.InteropServices.Guid("039b4c68-60fb-4b38-8f1f-e9e093618bc6")]

namespace Winder {
  public class PlugIn : Rhino.PlugIns.PlugIn {
    public PlugIn() {
      Instance = this;
    }

    public static PlugIn Instance { get; private set; }
  }

  [Rhino.Commands.CommandStyle(Rhino.Commands.Style.ScriptRunner)]
  
  public class Command : Rhino.Commands.Command {
    public Command() {
      Instance = this;
    }

    public static Command Instance { get; private set; }

    public override System.String EnglishName => "Winder";

    protected override Rhino.Commands.Result RunCommand(Rhino.RhinoDoc activeDocument, Rhino.Commands.RunMode runMode) {
      return Rhino.Commands.Result.Success;
    }
  }
}