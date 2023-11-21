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
      var selectedObjects = new System.Collections.Generic.List<Rhino.DocObjects.RhinoObject>(
        Rhino.RhinoDoc.ActiveDoc.Objects.GetSelectedObjects(false, false)
      );

      if (selectedObjects.Count < 2) {
        Rhino.RhinoApp.WriteLine("Winder: Select some objects to run the command");
        return Rhino.Commands.Result.Nothing;
      }

      // Rhino.RhinoApp.RunScript("!_Join", false);
      Rhino.RhinoApp.RunScript("!_Explode", true);

      var unhandledLayerIndex = Winder.Helper.CreateOrGetLayerIndex("Winder Unhandled Layer", 255, 255, 255);
      var undefinedLayerIndex = Winder.Helper.CreateOrGetLayerIndex("Winder Undefined Layer", 255, 0, 0);
      var unflippedLayerIndex = Winder.Helper.CreateOrGetLayerIndex("Winder Unflipped Layer", 0, 255, 0);
      var flippedLayerIndex = Winder.Helper.CreateOrGetLayerIndex("Winder Flipped Layer", 0, 0, 255);
      var deductedLayerIndex = Winder.Helper.CreateOrGetLayerIndex("Winder Deducted Layer", 255, 16, 240);
      var interactiveLayerIndex = Winder.Helper.CreateOrGetLayerIndex("Winder Interactive Layer", 0, 255, 255);

      var interactiveAttributes = new Rhino.DocObjects.ObjectAttributes {
        LayerIndex = interactiveLayerIndex
      };
      
      var explodedObjects = new System.Collections.Generic.List<Rhino.DocObjects.RhinoObject>(
        Rhino.RhinoDoc.ActiveDoc.Objects.GetSelectedObjects(false, false)
      );

      for (var objectIndex = 0; objectIndex < explodedObjects.Count; objectIndex++) {
        var originalObject = explodedObjects[objectIndex];

        var objectAttributes = originalObject.Attributes;
        var objectGeometry = originalObject.Geometry;

        objectAttributes.LayerIndex = unhandledLayerIndex;

        var modifyedObjectGuid = Rhino.RhinoDoc.ActiveDoc.Objects.Add(objectGeometry, objectAttributes);
        var modifyedObject = Rhino.RhinoDoc.ActiveDoc.Objects.Find(modifyedObjectGuid);

        explodedObjects[objectIndex] = modifyedObject;

        var wasDeletionSucceeded = Rhino.RhinoDoc.ActiveDoc.Objects.Delete(originalObject);

        if (wasDeletionSucceeded == false) {
          Rhino.RhinoApp.WriteLine("Winder: Ssome object could not be deleted");
          return Rhino.Commands.Result.Failure;
        }

        Rhino.RhinoDoc.ActiveDoc.Views.Redraw();
      }

      foreach (var documentLayer in Rhino.RhinoDoc.ActiveDoc.Layers) {
        if (documentLayer.Index == unhandledLayerIndex) continue;
        if (documentLayer.Index == undefinedLayerIndex) continue;
        if (documentLayer.Index == unflippedLayerIndex) continue;
        if (documentLayer.Index == flippedLayerIndex) continue;
        if (documentLayer.Index == deductedLayerIndex) continue;
        if (documentLayer.Index == interactiveLayerIndex) continue;

        Rhino.RhinoDoc.ActiveDoc.Layers.Delete(documentLayer);
      }

      var boundaryObjects = new System.Collections.Generic.List<Rhino.DocObjects.BrepObject>();

      var explodedBoundaries = new System.Collections.Generic.List<Rhino.Geometry.Brep>();
      var joinedBoundaries = new System.Collections.Generic.List<Rhino.Geometry.Brep>();

      foreach (var explodedObject in explodedObjects) {
        if (explodedObject.ObjectType == Rhino.DocObjects.ObjectType.Brep) {
          var boundaryObject = explodedObject as Rhino.DocObjects.BrepObject;
          var boundaryGeometry = boundaryObject.BrepGeometry;

          boundaryObjects.Add(boundaryObject);
          explodedBoundaries.Add(boundaryGeometry);
        }
      }

      var boundariesJunction = Rhino.Geometry.Brep.JoinBreps(explodedBoundaries, Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);

      if (boundariesJunction == null) {
        Rhino.RhinoApp.WriteLine("Winder: Boundary geometries could not be joined");
        return Rhino.Commands.Result.Failure;
      }

      foreach (var joinedBoundary in boundariesJunction) {
        joinedBoundaries.Add(joinedBoundary);
      }

      var unitedBoundingBox = Rhino.Geometry.BoundingBox.Empty;
      var boundingCentroids = new System.Collections.Generic.List<Rhino.Geometry.Point3d>();

      for (var objectIndex = 0; objectIndex < boundaryObjects.Count; objectIndex++) {
        var boundaryObject = boundaryObjects[objectIndex];

        var objectBoundingBox = boundaryObject.Geometry.GetBoundingBox(true);
        var boundingBoxCenter = objectBoundingBox.Center;

        unitedBoundingBox.Union(objectBoundingBox);
        boundingCentroids.Add(boundingBoxCenter);
      }

      var unionDiagonalLength = unitedBoundingBox.Diagonal.Length;
      var unionCenter = unitedBoundingBox.Center;

      for (var objectIndex = 0; objectIndex < boundaryObjects.Count; objectIndex++) {
        var boundaryObject = boundaryObjects[objectIndex];

        var boundaryFace = boundaryObject.BrepGeometry.Faces[0];
        var boundingCentroid = boundingCentroids[objectIndex];

        var newAttributes = boundaryObject.Attributes;
        var newGeometry = boundaryObject.BrepGeometry;

        var boundaryCentroid = boundaryObject.BrepGeometry.ClosestPoint(boundingCentroid);
        var wasBoundaryCentroidFound = boundaryFace.ClosestPoint(boundingCentroid, out double centroidPointU, out double centroidPointV);

        if (boundaryCentroid == Rhino.Geometry.Point3d.Unset || wasBoundaryCentroidFound == false) {
          newAttributes.LayerIndex = undefinedLayerIndex;

          Rhino.RhinoDoc.ActiveDoc.Objects.Delete(boundaryObject);
          Rhino.RhinoDoc.ActiveDoc.Objects.Add(newGeometry, newAttributes);

          Rhino.RhinoDoc.ActiveDoc.Views.Redraw();

          continue;
        }

        var centroidNormalVector = boundaryFace.NormalAt(centroidPointU, centroidPointV);

        var positiveNormalSegment = new Rhino.Geometry.Line(boundaryCentroid, centroidNormalVector, unionDiagonalLength);
        var negativeNormalSegment = new Rhino.Geometry.Line(boundaryCentroid, centroidNormalVector, unionDiagonalLength);

        var normalLineMax = new Rhino.Geometry.Point3d(positiveNormalSegment.ToX, positiveNormalSegment.ToY, positiveNormalSegment.ToZ);
        var normalLineMin = new Rhino.Geometry.Point3d(negativeNormalSegment.ToX, negativeNormalSegment.ToY, negativeNormalSegment.ToZ);

        var normalLine = new Rhino.Geometry.Line(normalLineMin, normalLineMax);
        Rhino.Geometry.Curve normalCurve = normalLine.ToNurbsCurve();

        Rhino.RhinoDoc.ActiveDoc.Objects.AddCurve(normalCurve, interactiveAttributes);
        Rhino.RhinoDoc.ActiveDoc.Views.Redraw();

        var importantPoints = new System.Collections.Generic.List<Rhino.Geometry.Point3d> {normalLineMin, normalLineMax};

        foreach (Rhino.Geometry.Brep joinedBoundary in joinedBoundaries) {
          Rhino.Geometry.Intersect.Intersection.CurveBrep(
            normalCurve,
            joinedBoundary,
            Rhino.RhinoMath.ZeroTolerance,
            out Rhino.Geometry.Curve[] overlapCurves,
            out Rhino.Geometry.Point3d[] intersectionPoints
          );

          foreach (Rhino.Geometry.Point3d intersectionPoint in intersectionPoints) {
            importantPoints.Add(intersectionPoint);
          }
        }

        var uniquePoints = new System.Collections.Generic.List<Rhino.Geometry.Point3d>(
          new System.Collections.Generic.HashSet<Rhino.Geometry.Point3d>(importantPoints)
        );

        if (uniquePoints.Count > 2 && uniquePoints.Count % 2 == 0) {
          var alignedPoints = new System.Collections.Generic.List<Rhino.Geometry.Point3d>(uniquePoints);
          alignedPoints.Sort((point1, point2) => point1.DistanceTo(normalLineMin).CompareTo(point2.DistanceTo(normalLineMin)));

          var isNextSegmentInside = false;

          for (int pointIndex = 0; pointIndex < alignedPoints.Count - 1; pointIndex++) {
            var analyzingPoint = alignedPoints[pointIndex];

            if (analyzingPoint.DistanceTo(boundaryCentroid) < Winder.Helper.MaximumDistanceTolerance) {
              var comparativeIndex = isNextSegmentInside ? +1 : -1;
              var comparativePoint = alignedPoints[pointIndex + comparativeIndex];

              var desiredVector = new Rhino.Geometry.Vector3d(
                comparativePoint.X - analyzingPoint.X,
                comparativePoint.Y - analyzingPoint.Y,
                comparativePoint.Z - analyzingPoint.Z
              );

              var dotProduct = centroidNormalVector * desiredVector;

              if (dotProduct > 0) {
                newGeometry.Flip();
                newAttributes.LayerIndex = flippedLayerIndex;
              }

              if (dotProduct < 0) {
                newAttributes.LayerIndex = unflippedLayerIndex;
              }

              if (dotProduct == 0) {
                newAttributes.LayerIndex = undefinedLayerIndex;
              }

              Rhino.RhinoDoc.ActiveDoc.Objects.Delete(boundaryObject);
              Rhino.RhinoDoc.ActiveDoc.Objects.Add(newGeometry, newAttributes);

              Rhino.RhinoDoc.ActiveDoc.Views.Redraw();
            }

            isNextSegmentInside = !isNextSegmentInside;
          }
        }
      
        if (uniquePoints.Count > 2 && uniquePoints.Count % 2 != 0) {
          var centerCentroidVector = new Rhino.Geometry.Vector3d(
            boundaryCentroid.X - unionCenter.X,
            boundaryCentroid.Y - unionCenter.Y,
            boundaryCentroid.Z - unionCenter.Z
          );

          centroidNormalVector.Unitize();

          var summationVector = centerCentroidVector + centroidNormalVector;
          var subtractionVector = centerCentroidVector - centroidNormalVector;

          if (summationVector.Length < subtractionVector.Length) {
            newGeometry.Flip();
            newAttributes.LayerIndex = deductedLayerIndex;
          }

          if (summationVector.Length > subtractionVector.Length) {
            newAttributes.LayerIndex = deductedLayerIndex;
          }

          if (summationVector.Length == subtractionVector.Length) {
            newAttributes.LayerIndex = undefinedLayerIndex;
          }

          Rhino.RhinoDoc.ActiveDoc.Objects.Delete(boundaryObject);
          Rhino.RhinoDoc.ActiveDoc.Objects.Add(newGeometry, newAttributes);

          Rhino.RhinoDoc.ActiveDoc.Views.Redraw();
        }

        if (uniquePoints.Count <= 2) {
          newAttributes.LayerIndex = undefinedLayerIndex;

          Rhino.RhinoDoc.ActiveDoc.Objects.Delete(boundaryObject);
          Rhino.RhinoDoc.ActiveDoc.Objects.Add(newGeometry, newAttributes);

          Rhino.RhinoDoc.ActiveDoc.Views.Redraw();
        }
      }

      Rhino.RhinoApp.WriteLine("Winder: Finished normal vectors harmonization");
      return Rhino.Commands.Result.Success;
    }
  }

  public class Helper {
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

    public static readonly double MaximumDistanceTolerance = 0.001;
  }
}