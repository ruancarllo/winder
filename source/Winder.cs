using System.Linq.Expressions;
using System.Runtime.InteropServices;
using Rhino.Geometry;

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
      var selectedObjectsEnumerable = Rhino.RhinoDoc.ActiveDoc.Objects.GetSelectedObjects(false, false);
      var selectedObjectsList = new System.Collections.Generic.List<Rhino.DocObjects.RhinoObject>(selectedObjectsEnumerable);

      if (selectedObjectsList.Count == 0) {
        Rhino.RhinoApp.WriteLine("Select some objects to run Winder command");
        return Rhino.Commands.Result.Nothing;
      };      

      Rhino.RhinoApp.RunScript("!_Join", false);
      Rhino.RhinoApp.RunScript("!_Explode", false);

      var explodedObjectsEnumberable = Rhino.RhinoDoc.ActiveDoc.Objects.GetSelectedObjects(false, false);
      var explodedObjectsList = new System.Collections.Generic.List<Rhino.DocObjects.RhinoObject>(explodedObjectsEnumberable);

      var boundaryObjects = new System.Collections.Generic.List<Rhino.DocObjects.BrepObject>();
      var boundaryGeometries = new System.Collections.Generic.List<Rhino.Geometry.Brep>();
      var joinedGeometries = new System.Collections.Generic.List<Rhino.Geometry.Brep>();

      foreach (var explodedObject in explodedObjectsList) {
        if (explodedObject.ObjectType == Rhino.DocObjects.ObjectType.Brep) {
          var boundaryObject = explodedObject as Rhino.DocObjects.BrepObject;

          boundaryObjects.Add(boundaryObject);
          boundaryGeometries.Add(boundaryObject.BrepGeometry);
        }
      }

      var geometriesJunction = Rhino.Geometry.Brep.JoinBreps(boundaryGeometries, Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);

      if (geometriesJunction == null) {
        Rhino.RhinoApp.WriteLine("Winder could not join the exploded objects");
        return Rhino.Commands.Result.Failure;
      }

      foreach (var joinedGeometry in geometriesJunction) {
        joinedGeometries.Add(joinedGeometry);
      }

      var unitedBoundingBox = Rhino.Geometry.BoundingBox.Empty;
      var separatedBoundingBoxes = new System.Collections.Generic.List<Rhino.Geometry.BoundingBox>();

      for (var boundaryObjectIndex = 0; boundaryObjectIndex < boundaryObjects.Count; boundaryObjectIndex++) {
        var boundaryObject = boundaryObjects[boundaryObjectIndex];
        var objectBoundingBox = boundaryObject.Geometry.GetBoundingBox(true);

        unitedBoundingBox.Union(objectBoundingBox);
        separatedBoundingBoxes.Add(objectBoundingBox);
      }

      var boundaryDiagonalLength = unitedBoundingBox.Diagonal.Length;
      var boundaryGlobalCenter = unitedBoundingBox.Center;

      var wrongLayerIndex = WinderLogistics.StaticMechanics.CreateOrGetLayerIndex("Winder Wrong Layer", 255, 0, 0);
      var correctLayerIndex = WinderLogistics.StaticMechanics.CreateOrGetLayerIndex("Winder Correct Layer", 0, 255, 0);
      var modifyedLayerIndex = WinderLogistics.StaticMechanics.CreateOrGetLayerIndex("Winder Modifyed Layer", 0, 0, 255);
      var deductedLayerIndex = WinderLogistics.StaticMechanics.CreateOrGetLayerIndex("Winder Deducted Layer", 255, 255, 0);
      var interactiveLayerIndex = WinderLogistics.StaticMechanics.CreateOrGetLayerIndex("Winder Interactive Layer", 0, 255, 255);

      var curveAttributes = new Rhino.DocObjects.ObjectAttributes {
        LayerIndex = interactiveLayerIndex
      };

      for (var boundaryObjectIndex = 0; boundaryObjectIndex < boundaryObjects.Count; boundaryObjectIndex++) {
        var boundaryObject = boundaryObjects[boundaryObjectIndex];
        var boundaryFace = boundaryObject.BrepGeometry.Faces[0];

        var objectBoundingBox = separatedBoundingBoxes[boundaryObjectIndex];

        var newAttributes = boundaryObject.Attributes;
        var newGeometry = boundaryObject.BrepGeometry;

        var doesRandomNormalInterceptEvenGeometries = false;
        var generatedRandomNormalCount = 0;
        var canBreakLoop = false;

        while (doesRandomNormalInterceptEvenGeometries == false) {
          generatedRandomNormalCount++;

          var randomGenerator = new System.Random();

          var randomBoundingPoint = new Rhino.Geometry.Point3d(
            objectBoundingBox.Min.X + randomGenerator.NextDouble() * (objectBoundingBox.Max.X - objectBoundingBox.Min.X) * 0.95,
            objectBoundingBox.Min.Y + randomGenerator.NextDouble() * (objectBoundingBox.Max.Y - objectBoundingBox.Min.Y) * 0.95,
            objectBoundingBox.Min.Z + randomGenerator.NextDouble() * (objectBoundingBox.Max.Z - objectBoundingBox.Min.Z) * 0.95
          );

          var randomBoundaryPoint = boundaryObject.BrepGeometry.ClosestPoint(randomBoundingPoint);

          var hasClosestPointBeenFound = boundaryFace.ClosestPoint(randomBoundingPoint, out double u, out double v);

          if (randomBoundaryPoint == Rhino.Geometry.Point3d.Unset || hasClosestPointBeenFound == false) {
            continue;
          }

          var randomNormalVector = boundaryFace.NormalAt(u, v);

          var positiveNormalSegment = new Rhino.Geometry.Line(randomBoundaryPoint, randomNormalVector, +boundaryDiagonalLength);
          var negativeNormalSegment = new Rhino.Geometry.Line(randomBoundaryPoint, randomNormalVector, -boundaryDiagonalLength);

          var lineMax = new Rhino.Geometry.Point3d(positiveNormalSegment.ToX, positiveNormalSegment.ToY, positiveNormalSegment.ToZ);
          var lineMin = new Rhino.Geometry.Point3d(negativeNormalSegment.ToX, negativeNormalSegment.ToY, negativeNormalSegment.ToZ);

          var normalLine = new Rhino.Geometry.Line(lineMin, lineMax);
          var normalCurve = normalLine.ToNurbsCurve();

          var normalCurveGuid = Rhino.RhinoDoc.ActiveDoc.Objects.AddCurve(normalCurve, curveAttributes);
          Rhino.RhinoDoc.ActiveDoc.Views.Redraw();

          var importantPoints = new System.Collections.Generic.List<Rhino.Geometry.Point3d> {lineMin, lineMax};

          foreach (Rhino.Geometry.Brep joinedGeometry in joinedGeometries) {
            Rhino.Geometry.Intersect.Intersection.CurveBrep(
              normalCurve,
              joinedGeometry,
              Rhino.RhinoMath.ZeroTolerance,
              out Rhino.Geometry.Curve[] overlapCurves,
              out Rhino.Geometry.Point3d[] intersectionPoints
            );

            foreach (Rhino.Geometry.Point3d intersectionPoint in intersectionPoints) {
              importantPoints.Add(intersectionPoint);
            }
          }

          if (importantPoints.Count > 2 && importantPoints.Count % 2 == 0) {
            doesRandomNormalInterceptEvenGeometries = true;

            System.Collections.Generic.List<Rhino.Geometry.Point3d> alignedPoints = new System.Collections.Generic.List<Rhino.Geometry.Point3d>(importantPoints);
            
            alignedPoints.Sort(
              (Rhino.Geometry.Point3d point1, Rhino.Geometry.Point3d point2) => point1.DistanceTo(lineMin).CompareTo(point2.DistanceTo(lineMin))
            );

            System.Boolean isNextSegmentInside = false;

            for (System.Int32 alignedPointIndex = 0; alignedPointIndex < alignedPoints.Count - 1; alignedPointIndex++) {
              Rhino.Geometry.Point3d analyzingPoint = alignedPoints[alignedPointIndex];

              if (analyzingPoint.DistanceTo(randomBoundaryPoint) < WinderLogistics.StaticMechanics.MaximumDistanceTolerance) {
                System.Double dotProduct = 0.0;

                if (isNextSegmentInside) {
                  Rhino.Geometry.Point3d posteriorPoint = alignedPoints[alignedPointIndex + 1];

                  Rhino.Geometry.Vector3d desiredVector = new Rhino.Geometry.Vector3d(
                    posteriorPoint.X - analyzingPoint.X,
                    posteriorPoint.Y - analyzingPoint.Y,
                    posteriorPoint.Z - analyzingPoint.Z
                  );

                  dotProduct = randomNormalVector * desiredVector;
                }

                else {
                  Rhino.Geometry.Point3d anteriorPoint = alignedPoints[alignedPointIndex - 1];

                  Rhino.Geometry.Vector3d desiredVector = new Rhino.Geometry.Vector3d(
                    anteriorPoint.X - analyzingPoint.X,
                    anteriorPoint.Y - analyzingPoint.Y,
                    anteriorPoint.Z - analyzingPoint.Z
                  );

                  dotProduct = randomNormalVector * desiredVector;
                }

                if (dotProduct > 0) {
                  newGeometry.Flip();

                  newAttributes.LayerIndex = modifyedLayerIndex;
                }

                if (dotProduct < 0) {
                  newAttributes.LayerIndex = correctLayerIndex;
                }

                if (dotProduct == 0) {
                  newAttributes.LayerIndex = wrongLayerIndex;
                }

                Rhino.RhinoDoc.ActiveDoc.Objects.Delete(boundaryObject);
                Rhino.RhinoDoc.ActiveDoc.Objects.Add(newGeometry, newAttributes);
              }

              isNextSegmentInside = !isNextSegmentInside;
            }
          }

          if (generatedRandomNormalCount == StaticMechanics.MaximumNormalCount) {
            canBreakLoop = true;

            var centerVector = new Rhino.Geometry.Vector3d(
              randomBoundaryPoint.X - boundaryGlobalCenter.X,
              randomBoundaryPoint.Y - boundaryGlobalCenter.Y,
              randomBoundaryPoint.Z - boundaryGlobalCenter.Z
            );

            randomNormalVector.Unitize();

            var summationVector = centerVector + randomNormalVector;
            var subtractionVector = centerVector - randomNormalVector;

            if (summationVector.Length < subtractionVector.Length) {
              newGeometry.Flip();

              newAttributes.LayerIndex = deductedLayerIndex;
            }

            if (summationVector.Length > subtractionVector.Length) {
              newAttributes.LayerIndex = correctLayerIndex;
            }

            if (summationVector.Length == subtractionVector.Length) {
              newAttributes.LayerIndex = wrongLayerIndex;
            }

            Rhino.RhinoDoc.ActiveDoc.Objects.Delete(boundaryObject);
            Rhino.RhinoDoc.ActiveDoc.Objects.Add(newGeometry, newAttributes);
          }
        
          Rhino.RhinoDoc.ActiveDoc.Objects.Delete(normalCurveGuid, true);
          Rhino.RhinoDoc.ActiveDoc.Views.Redraw();

          if (canBreakLoop) {
            break;
          }
        }

        Rhino.RhinoDoc.ActiveDoc.Views.Redraw();
      }

      Rhino.RhinoApp.WriteLine("Finished running Winder Command");

      return Rhino.Commands.Result.Success;
    }
  }

  public class StaticMechanics {
    public static readonly int MaximumNormalCount = 20;
    public static readonly double MaximumDistanceTolerance = 0.001;

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
  }
}