using System.Runtime.InteropServices;

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
      System.Collections.Generic.List<Rhino.DocObjects.RhinoObject> selectedObjects = new System.Collections.Generic.List<Rhino.DocObjects.RhinoObject>(
        Rhino.RhinoDoc.ActiveDoc.Objects.GetSelectedObjects(false, false)
      );

      if (selectedObjects.Count < 2) {
        Rhino.RhinoApp.WriteLine("Winder: Select some objects to run the command");
        return Rhino.Commands.Result.Nothing;
      }

      Rhino.RhinoApp.RunScript("!_Join", false);
      Rhino.RhinoApp.RunScript("!_Explode", true);

      System.Int32 unhandledLayerIndex = Winder.Helpers.CreateOrGetLayerIndex("Winder Unhandled Layer", 255, 255, 255);
      System.Int32 undefinedLayerIndex = Winder.Helpers.CreateOrGetLayerIndex("Winder Undefined Layer", 255, 0, 0);
      System.Int32 unflippedLayerIndex = Winder.Helpers.CreateOrGetLayerIndex("Winder Unflipped Layer", 0, 255, 0);
      System.Int32 flippedLayerIndex = Winder.Helpers.CreateOrGetLayerIndex("Winder Flipped Layer", 0, 0, 255);
      System.Int32 deductedLayerIndex = Winder.Helpers.CreateOrGetLayerIndex("Winder Deducted Layer", 255, 16, 240);
      System.Int32 interactiveLayerIndex = Winder.Helpers.CreateOrGetLayerIndex("Winder Interactive Layer", 0, 255, 255);

      Rhino.DocObjects.ObjectAttributes interactiveAttributes = new Rhino.DocObjects.ObjectAttributes {
        LayerIndex = interactiveLayerIndex
      };
      
      System.Collections.Generic.List<Rhino.DocObjects.RhinoObject> explodedObjects = new System.Collections.Generic.List<Rhino.DocObjects.RhinoObject>(
        Rhino.RhinoDoc.ActiveDoc.Objects.GetSelectedObjects(false, false)
      );

      for (System.Int32 objectIndex = 0; objectIndex < explodedObjects.Count; objectIndex++) {
        Rhino.DocObjects.RhinoObject originalObject = explodedObjects[objectIndex];

        Rhino.DocObjects.ObjectAttributes objectAttributes = originalObject.Attributes;
        Rhino.Geometry.GeometryBase objectGeometry = originalObject.Geometry;

        objectAttributes.LayerIndex = unhandledLayerIndex;

        System.Guid modifyedObjectGuid = Rhino.RhinoDoc.ActiveDoc.Objects.Add(objectGeometry, objectAttributes);
        Rhino.DocObjects.RhinoObject modifyedObject = Rhino.RhinoDoc.ActiveDoc.Objects.Find(modifyedObjectGuid);

        explodedObjects[objectIndex] = modifyedObject;

        System.Boolean wasDeletionSucceeded = Rhino.RhinoDoc.ActiveDoc.Objects.Delete(originalObject);

        if (wasDeletionSucceeded == false) {
          Rhino.RhinoApp.WriteLine("Winder: Ssome object could not be deleted");
          return Rhino.Commands.Result.Failure;
        }

        Rhino.RhinoDoc.ActiveDoc.Views.Redraw();
      }

      foreach (Rhino.DocObjects.Layer documentLayer in Rhino.RhinoDoc.ActiveDoc.Layers) {
        if (documentLayer.Index == unhandledLayerIndex) continue;
        if (documentLayer.Index == undefinedLayerIndex) continue;
        if (documentLayer.Index == unflippedLayerIndex) continue;
        if (documentLayer.Index == flippedLayerIndex) continue;
        if (documentLayer.Index == deductedLayerIndex) continue;
        if (documentLayer.Index == interactiveLayerIndex) continue;

        Rhino.RhinoDoc.ActiveDoc.Layers.Delete(documentLayer);
      }

      System.Collections.Generic.List<Rhino.DocObjects.BrepObject> boundaryObjects = new System.Collections.Generic.List<Rhino.DocObjects.BrepObject>();

      System.Collections.Generic.List<Rhino.Geometry.Brep> explodedBoundaries = new System.Collections.Generic.List<Rhino.Geometry.Brep>();
      System.Collections.Generic.List<Rhino.Geometry.Brep> joinedBoundaries = new System.Collections.Generic.List<Rhino.Geometry.Brep>();

      foreach (Rhino.DocObjects.RhinoObject explodedObject in explodedObjects) {
        if (explodedObject.ObjectType == Rhino.DocObjects.ObjectType.Brep) {
          Rhino.DocObjects.BrepObject boundaryObject = explodedObject as Rhino.DocObjects.BrepObject;
          Rhino.Geometry.Brep boundaryGeometry = boundaryObject.BrepGeometry;

          boundaryObjects.Add(boundaryObject);
          explodedBoundaries.Add(boundaryGeometry);
        }
      }

      Rhino.Geometry.Brep[] boundariesJunction = Rhino.Geometry.Brep.JoinBreps(explodedBoundaries, Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);

      if (boundariesJunction == null) {
        Rhino.RhinoApp.WriteLine("Winder: Boundary geometries could not be joined");
        return Rhino.Commands.Result.Failure;
      }

      foreach (Rhino.Geometry.Brep joinedBoundary in boundariesJunction) {
        joinedBoundaries.Add(joinedBoundary);
      }

      Rhino.Geometry.BoundingBox unitedBoundingBox = Rhino.Geometry.BoundingBox.Empty;
      System.Collections.Generic.List<Rhino.Geometry.Point3d> boundingCentroids = new System.Collections.Generic.List<Rhino.Geometry.Point3d>();

      for (System.Int32 objectIndex = 0; objectIndex < boundaryObjects.Count; objectIndex++) {
        Rhino.DocObjects.BrepObject boundaryObject = boundaryObjects[objectIndex];

        Rhino.Geometry.BoundingBox objectBoundingBox = boundaryObject.Geometry.GetBoundingBox(true);
        Rhino.Geometry.Point3d boundingBoxCenter = objectBoundingBox.Center;

        unitedBoundingBox.Union(objectBoundingBox);
        boundingCentroids.Add(boundingBoxCenter);
      }

      System.Double unionDiagonalLength = unitedBoundingBox.Diagonal.Length;
      Rhino.Geometry.Point3d unionCenter = unitedBoundingBox.Center;

      Rhino.Geometry.Vector3d inflationVector = new Rhino.Geometry.Vector3d(
        Winder.Helpers.DefaultInflationValue,
        Winder.Helpers.DefaultInflationValue,
        Winder.Helpers.DefaultInflationValue
      );

      Rhino.Geometry.BoundingBox inflatedBoundingBox = new Rhino.Geometry.BoundingBox(
        unitedBoundingBox.Min - inflationVector,
        unitedBoundingBox.Max + inflationVector
      );

      Rhino.RhinoDoc.ActiveDoc.Objects.Add(Rhino.Geometry.Brep.CreateFromBox(inflatedBoundingBox));
      Rhino.RhinoDoc.ActiveDoc.Views.Redraw();

      return Rhino.Commands.Result.Success;
    }
  }

  public class Helpers {
    public static System.Int32 CreateOrGetLayerIndex(System.String name, System.Int16 red, System.Int16 green, System.Int16 blue) {
      Rhino.DocObjects.Layer existingLayer = Rhino.RhinoDoc.ActiveDoc.Layers.FindName(name);

      if (existingLayer == null) {
        Rhino.DocObjects.Layer newLayer = new Rhino.DocObjects.Layer() {
          Name = name,
          Color = System.Drawing.Color.FromArgb(red, green, blue),
          IsVisible = true,
          IsLocked = false
        };

        System.Int32 layerIndex = Rhino.RhinoDoc.ActiveDoc.Layers.Add(newLayer);
        return layerIndex;
      }

      else {
        System.Int32 layerIndex = existingLayer.Index;
        return layerIndex;
      }
    }

    public static readonly System.Double DefaultInflationValue = 10;
    public static readonly System.Double MaximumDistanceTolerance = 0.001;
  }
}