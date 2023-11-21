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

      if (selectedObjectsList.Count < 2) {
        Rhino.RhinoApp.WriteLine("Winder: Select some objects to run the command");
        return Rhino.Commands.Result.Nothing;
      }

      // Rhino.RhinoApp.RunScript("!_Join", false);
      Rhino.RhinoApp.RunScript("!_Explode", true);

      var wrongLayerIndex = WinderLogistics.StaticMechanisms.CreateOrGetLayerIndex("Winder Wrong Layer", 255, 0, 0);
      var correctLayerIndex = WinderLogistics.StaticMechanisms.CreateOrGetLayerIndex("Winder Correct Layer", 0, 255, 0);
      var flippedLayerIndex = WinderLogistics.StaticMechanisms.CreateOrGetLayerIndex("Winder Flipped Layer", 0, 0, 255);
      var deductedLayerIndex = WinderLogistics.StaticMechanisms.CreateOrGetLayerIndex("Winder Deducted Layer", 255, 16, 240);
      var unhandledLayerIndex = WinderLogistics.StaticMechanisms.CreateOrGetLayerIndex("Winder Unhandled Layer", 255, 255, 255);
      var interactiveLayerIndex = WinderLogistics.StaticMechanisms.CreateOrGetLayerIndex("Winder Interactive Layer", 0, 255, 255);

      var defaultInteractiveAttributes = new Rhino.DocObjects.ObjectAttributes {
        LayerIndex = interactiveLayerIndex
      };
      
      var explodedObjectsEnumerable = Rhino.RhinoDoc.ActiveDoc.Objects.GetSelectedObjects(false, false);
      var explodedObjectsList = new System.Collections.Generic.List<Rhino.DocObjects.RhinoObject>(explodedObjectsEnumerable);

      var boundaryObjectsFilteredList = new System.Collections.Generic.List<Rhino.DocObjects.BrepObject>();

      var explodedBoundaryGeometries = new System.Collections.Generic.List<Rhino.Geometry.Brep>();
      var joinedBoundaryGeometries = new System.Collections.Generic.List<Rhino.Geometry.Brep>();

      for (var explodedObjectIndex = 0; explodedObjectIndex < explodedObjectsList.Count; explodedObjectIndex++) {
        var originaObject = explodedObjectsList[explodedObjectIndex];

        var objectAttributes = originaObject.Attributes;
        var objectGeometry = originaObject.Geometry;

        objectAttributes.LayerIndex = unhandledLayerIndex;

        var modifyedObjectGuid = Rhino.RhinoDoc.ActiveDoc.Objects.Add(objectGeometry, objectAttributes);
        var modifyedObject = Rhino.RhinoDoc.ActiveDoc.Objects.Find(modifyedObjectGuid);

        explodedObjectsList[explodedObjectIndex] = modifyedObject;

        var wasDeletionSucceeded = Rhino.RhinoDoc.ActiveDoc.Objects.Delete(originaObject);
        
        Rhino.RhinoDoc.ActiveDoc.Views.Redraw();

        if (wasDeletionSucceeded == false) {
          Rhino.RhinoApp.WriteLine("Winder: Ssome object could not be deleted");
          return Rhino.Commands.Result.Failure;
        }
      }

      foreach (var documentLayer in Rhino.RhinoDoc.ActiveDoc.Layers) {
        if (documentLayer.Index == wrongLayerIndex) continue;
        if (documentLayer.Index == correctLayerIndex) continue;
        if (documentLayer.Index == flippedLayerIndex) continue;
        if (documentLayer.Index == deductedLayerIndex) continue;
        if (documentLayer.Index == unhandledLayerIndex) continue;
        if (documentLayer.Index == interactiveLayerIndex) continue;

        Rhino.RhinoDoc.ActiveDoc.Layers.Delete(documentLayer);
      }

      foreach (var explodedObject in explodedObjectsList) {
        if (explodedObject.ObjectType == Rhino.DocObjects.ObjectType.Brep) {
          var boundaryObject = explodedObject as Rhino.DocObjects.BrepObject;
          var boundaryGeometry = boundaryObject.BrepGeometry;

          boundaryObjectsFilteredList.Add(boundaryObject);
          explodedBoundaryGeometries.Add(boundaryGeometry);
        }
      }

      var boundaryGeometriesJunction = Rhino.Geometry.Brep.JoinBreps(explodedBoundaryGeometries, Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);

      if (boundaryGeometriesJunction == null) {
        Rhino.RhinoApp.WriteLine("Winder: Boundary geometries could not be joined");
        return Rhino.Commands.Result.Failure;
      }

      foreach (var joinedGeometry in boundaryGeometriesJunction) {
        joinedBoundaryGeometries.Add(joinedGeometry);
      }

      var unitedBoundingBox = Rhino.Geometry.BoundingBox.Empty;
      var separatedBoundingBoxes = new System.Collections.Generic.List<Rhino.Geometry.BoundingBox>();

      for (var boundaryObjectIndex = 0; boundaryObjectIndex < boundaryObjectsFilteredList.Count; boundaryObjectIndex++) {
        var boundaryObject = boundaryObjectsFilteredList[boundaryObjectIndex];

        var objectBoundingBox = boundaryObject.Geometry.GetBoundingBox(true);

        unitedBoundingBox.Union(objectBoundingBox);
        separatedBoundingBoxes.Add(objectBoundingBox);
      }

      var boundaryUnionDiagonalLength = unitedBoundingBox.Diagonal.Length;
      var boundaryUnionCenter = unitedBoundingBox.Center;

      for (var boundaryObjectIndex = 0; boundaryObjectIndex < boundaryObjectsFilteredList.Count; boundaryObjectIndex++) {
        var boundaryObject = boundaryObjectsFilteredList[boundaryObjectIndex];
        var boundaryFace = boundaryObject.BrepGeometry.Faces[0];

        var boundingBoxCenter = separatedBoundingBoxes[boundaryObjectIndex].Center;

        var newAttributes = boundaryObject.Attributes;
        var newGeometry = boundaryObject.BrepGeometry;

        var boundaryCentroidPoint = boundaryObject.BrepGeometry.ClosestPoint(boundingBoxCenter);
        var wasCentroidPointFound = boundaryFace.ClosestPoint(boundingBoxCenter, out double centroidPointU, out double centroidPointV);

        if (boundaryCentroidPoint == Rhino.Geometry.Point3d.Unset || wasCentroidPointFound == false) {
          newAttributes.LayerIndex = wrongLayerIndex;

          Rhino.RhinoDoc.ActiveDoc.Objects.Delete(boundaryObject);
          Rhino.RhinoDoc.ActiveDoc.Objects.Add(newGeometry, newAttributes);

          continue;
        }

        var centroidNormalVector = boundaryFace.NormalAt(centroidPointU, centroidPointV);
        var couldUnitizeNormalVector = centroidNormalVector.Unitize();

        if (couldUnitizeNormalVector == false) {
          newAttributes.LayerIndex = wrongLayerIndex;

          Rhino.RhinoDoc.ActiveDoc.Objects.Delete(boundaryObject);
          Rhino.RhinoDoc.ActiveDoc.Objects.Add(newGeometry, newAttributes);

          continue;
        }

        var centerCentroidVector = boundaryCentroidPoint - boundaryUnionCenter;
        var couldUnitizeCenterCentroidVector = centerCentroidVector.Unitize();

        if (couldUnitizeCenterCentroidVector == false) {
          newAttributes.LayerIndex = wrongLayerIndex;

          Rhino.RhinoDoc.ActiveDoc.Objects.Delete(boundaryObject);
          Rhino.RhinoDoc.ActiveDoc.Objects.Add(newGeometry, newAttributes);

          continue;
        }

        var centerCentroidLine = new Rhino.Geometry.Line(boundaryUnionCenter, centerCentroidVector * boundaryUnionDiagonalLength);
        Rhino.Geometry.Curve centerCentroidCurve = centerCentroidLine.ToNurbsCurve();

        var centerCentroidCurveMax = centerCentroidLine.To;
        
        var centerCentroidCurveGuid = Rhino.RhinoDoc.ActiveDoc.Objects.AddCurve(centerCentroidCurve, defaultInteractiveAttributes);
        Rhino.RhinoDoc.ActiveDoc.Views.Redraw();

        var importantPointsList = new System.Collections.Generic.List<Rhino.Geometry.Point3d> {
          boundaryUnionCenter,
          centerCentroidCurveMax
        };

        foreach (Rhino.Geometry.Brep joinedBoundaryGeometry in joinedBoundaryGeometries) {
          Rhino.Geometry.Intersect.Intersection.CurveBrep(
            centerCentroidCurve,
            joinedBoundaryGeometry,
            Rhino.RhinoMath.ZeroTolerance,
            out Rhino.Geometry.Curve[] overlapCurves,
            out Rhino.Geometry.Point3d[] intersectionPoints
          );

          foreach (Rhino.Geometry.Point3d intersectionPoint in intersectionPoints) {
            importantPointsList.Add(intersectionPoint);
          }
        }

        var uniquePointsHashSet = new System.Collections.Generic.HashSet<Rhino.Geometry.Point3d>(importantPointsList);
          
        var uniqueImportantPointsList = new System.Collections.Generic.List<Rhino.Geometry.Point3d>(uniquePointsHashSet);

        if (uniqueImportantPointsList.Count % 2 == 0) {
          System.Collections.Generic.List<Rhino.Geometry.Point3d> alignedPointsList = new System.Collections.Generic.List<Rhino.Geometry.Point3d>(uniqueImportantPointsList);
          
          alignedPointsList.Sort(
            (Rhino.Geometry.Point3d point1, Rhino.Geometry.Point3d point2) => point1.DistanceTo(centerCentroidCurveMax).CompareTo(point2.DistanceTo(centerCentroidCurveMax))
          );

          System.Boolean isNextSegmentInside = false;

          for (System.Int32 alignedPointIndex = 0; alignedPointIndex < alignedPointsList.Count - 1; alignedPointIndex++) {
            Rhino.Geometry.Point3d analyzingPoint = alignedPointsList[alignedPointIndex];

            if (analyzingPoint.DistanceTo(boundaryCentroidPoint) < WinderLogistics.StaticMechanisms.MaximumDistanceTolerance) {
              System.Double dotProduct = 0.0;

              if (isNextSegmentInside) {
                Rhino.Geometry.Point3d posteriorPoint = alignedPointsList[alignedPointIndex + 1];

                Rhino.Geometry.Vector3d desiredVector = new Rhino.Geometry.Vector3d(
                  posteriorPoint.X - analyzingPoint.X,
                  posteriorPoint.Y - analyzingPoint.Y,
                  posteriorPoint.Z - analyzingPoint.Z
                );

                dotProduct = centroidNormalVector * desiredVector;
              }

              else {
                Rhino.Geometry.Point3d anteriorPoint = alignedPointsList[alignedPointIndex - 1];

                Rhino.Geometry.Vector3d desiredVector = new Rhino.Geometry.Vector3d(
                  anteriorPoint.X - analyzingPoint.X,
                  anteriorPoint.Y - analyzingPoint.Y,
                  anteriorPoint.Z - analyzingPoint.Z
                );

                dotProduct = centroidNormalVector * desiredVector;
              }

              if (dotProduct > 0) {
                newGeometry.Flip();

                newAttributes.LayerIndex = flippedLayerIndex;
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
      
        else {
          centroidNormalVector.Unitize();

          var summationVector = centerCentroidVector + centroidNormalVector;
          var subtractionVector = centerCentroidVector - centroidNormalVector;

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

        // Rhino.RhinoDoc.ActiveDoc.Objects.Delete(centerCentroidCurveGuid, true);
        // Rhino.RhinoDoc.ActiveDoc.Views.Redraw();
      }

      return Rhino.Commands.Result.Success;
    }
  }

  public class StaticMechanisms {
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