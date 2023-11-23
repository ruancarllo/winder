namespace WinderHandlers {
  public class ProcessesExecutor {
    System.Collections.Generic.List<Rhino.DocObjects.RhinoObject> ExplodedSelectedObjects;
    System.Collections.Generic.List<Rhino.DocObjects.BrepObject> ExplodedBoundaryObjects;

    System.Collections.Generic.List<Rhino.Geometry.Brep> FragmentedBoundaries;
    System.Collections.Generic.List<Rhino.Geometry.Brep> ConnectedBoundaries;

    System.Collections.Generic.List<Rhino.Geometry.Point3d> BoundaryCollectionCentroids;
    Rhino.Geometry.BoundingBox BoundaryCollectionBoundingBox;
    Rhino.Geometry.Point3d BoundaryCollectionCenter;
    System.Double BoundaryCollectionDiagonalSize;

    System.Collections.Generic.List<Rhino.Geometry.Point3d> BoundaryIntegrationMidpoints;

    Rhino.DocObjects.ObjectAttributes InteractiveAttributes;

    System.Int32 InteractiveLayerIndex;
    System.Int32 UnhandledLayerIndex;
    System.Int32 UndefinedLayerIndex;
    System.Int32 UnflippedLayerIndex;
    System.Int32 DeductedLayerIndex;
    System.Int32 FlippedLayerIndex;

    public void VerifyObjectsSelection() {
      System.Collections.Generic.IEnumerable<Rhino.DocObjects.RhinoObject> objectsSelection = Rhino.RhinoDoc.ActiveDoc.Objects.GetSelectedObjects(false, false);
      System.Collections.Generic.List<Rhino.DocObjects.RhinoObject> selectedObjects = new System.Collections.Generic.List<Rhino.DocObjects.RhinoObject>(objectsSelection);

      if (selectedObjects.Count < 2) {
        throw new System.Exception("Winder: Select some objects to run the command");
      }

      Rhino.RhinoApp.WriteLine("Winder: Verified objects selection");
    }

    public void DefineEssentialLayers() {
      this.InteractiveLayerIndex = WinderHandlers.StaticBundles.CreateOrGetLayerIndex("Winder Interactive Layer", 0, 255, 255);
      this.UnhandledLayerIndex = WinderHandlers.StaticBundles.CreateOrGetLayerIndex("Winder Unhandled Layer", 255, 255, 255);
      this.UndefinedLayerIndex = WinderHandlers.StaticBundles.CreateOrGetLayerIndex("Winder Undefined Layer", 255, 0, 0);
      this.UnflippedLayerIndex = WinderHandlers.StaticBundles.CreateOrGetLayerIndex("Winder Unflipped Layer", 0, 255, 0);
      this.DeductedLayerIndex = WinderHandlers.StaticBundles.CreateOrGetLayerIndex("Winder Deducted Layer", 255, 16, 240);
      this.FlippedLayerIndex = WinderHandlers.StaticBundles.CreateOrGetLayerIndex("Winder Flipped Layer", 0, 0, 255);

      Rhino.RhinoApp.WriteLine("Winder: Defined essential layers");
    }

    public void DefineInteractiveAttributes() {
      this.InteractiveAttributes = new Rhino.DocObjects.ObjectAttributes {
        LayerIndex = this.InteractiveLayerIndex
      };

      Rhino.RhinoApp.WriteLine("Winder: Defined interactive attributes");
    }

    public void RegisterExplodedSelectedObjects() {
      System.Collections.Generic.IEnumerable<Rhino.DocObjects.RhinoObject> objectsSelection = Rhino.RhinoDoc.ActiveDoc.Objects.GetSelectedObjects(false, false);
      this.ExplodedSelectedObjects = new System.Collections.Generic.List<Rhino.DocObjects.RhinoObject>(objectsSelection);

      Rhino.RhinoApp.WriteLine("Winder: Registered exploded selected objects");
    }

    public void RepaintExplodedSelectedObjects() {
      for (System.Int32 objectIndex = 0; objectIndex < this.ExplodedSelectedObjects.Count; objectIndex++) {
        Rhino.DocObjects.RhinoObject originalObject = this.ExplodedSelectedObjects[objectIndex];

        Rhino.DocObjects.ObjectAttributes objectAttributes = originalObject.Attributes;
        Rhino.Geometry.GeometryBase objectGeometry = originalObject.Geometry;

        objectAttributes.LayerIndex = this.UnhandledLayerIndex;

        System.Guid modifyedObjectGuid = Rhino.RhinoDoc.ActiveDoc.Objects.Add(objectGeometry, objectAttributes);
        Rhino.DocObjects.RhinoObject modifyedObject = Rhino.RhinoDoc.ActiveDoc.Objects.Find(modifyedObjectGuid);

        this.ExplodedSelectedObjects[objectIndex] = modifyedObject;

        System.Boolean wasDeletionSucceeded = Rhino.RhinoDoc.ActiveDoc.Objects.Delete(originalObject);

        if (wasDeletionSucceeded == false) {
          throw new System.Exception("Winder: Ssome object could not be deleted");
        }

        Rhino.RhinoDoc.ActiveDoc.Views.Redraw();
      }

      Rhino.RhinoApp.WriteLine("Winder: Repainted exploded selected objects");
    }

    public void DeleteUnessentialLayers() {
      foreach (Rhino.DocObjects.Layer documentLayer in Rhino.RhinoDoc.ActiveDoc.Layers) {
        if (documentLayer.Index == this.InteractiveLayerIndex) continue;
        if (documentLayer.Index == this.UnhandledLayerIndex) continue;
        if (documentLayer.Index == this.UndefinedLayerIndex) continue;
        if (documentLayer.Index == this.UnflippedLayerIndex) continue;
        if (documentLayer.Index == this.DeductedLayerIndex) continue;
        if (documentLayer.Index == this.FlippedLayerIndex) continue;

        Rhino.RhinoDoc.ActiveDoc.Layers.Delete(documentLayer);
      }

      Rhino.RhinoApp.WriteLine("Winder: Deleted unessential layers");
    }

    public void FilterExplodedBoundaryObjects() {
      this.ExplodedBoundaryObjects = new System.Collections.Generic.List<Rhino.DocObjects.BrepObject>();

      foreach (Rhino.DocObjects.RhinoObject explodedObject in this.ExplodedSelectedObjects) {
        if (explodedObject.ObjectType == Rhino.DocObjects.ObjectType.Brep) {
          Rhino.DocObjects.BrepObject boundaryObject = explodedObject as Rhino.DocObjects.BrepObject;
          this.ExplodedBoundaryObjects.Add(boundaryObject);
        }
      }

      Rhino.RhinoApp.WriteLine("Winder: Filtered exploded boundary objects");
    }

    public void SetFragmentedBoundaries() {
      this.FragmentedBoundaries = new System.Collections.Generic.List<Rhino.Geometry.Brep>();

      foreach (Rhino.DocObjects.BrepObject boundaryObject in this.ExplodedBoundaryObjects) {
        Rhino.Geometry.Brep explodedBoundary = boundaryObject.BrepGeometry;
        this.FragmentedBoundaries.Add(explodedBoundary);
      }

      Rhino.RhinoApp.WriteLine("Winder: Setted fragmented boundaries");
    }

    public void SetConnectedBoundaries() {
      Rhino.Geometry.Brep[] connectedBoundaries = Rhino.Geometry.Brep.JoinBreps(this.FragmentedBoundaries, Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);

      if (connectedBoundaries == null) {
        throw new System.Exception("Winder: Boundary geometries could not be joined");
      }

      this.ConnectedBoundaries = new System.Collections.Generic.List<Rhino.Geometry.Brep>();

      foreach (Rhino.Geometry.Brep connectedBoundary in connectedBoundaries) {
        this.ConnectedBoundaries.Add(connectedBoundary);
      }

      Rhino.RhinoApp.WriteLine("Winder: Setted connected boundaries");
    }

    public void DefineBoundaryCollectionAttributes() {
      this.BoundaryCollectionBoundingBox = Rhino.Geometry.BoundingBox.Empty;
      this.BoundaryCollectionCentroids = new System.Collections.Generic.List<Rhino.Geometry.Point3d>();

      for (System.Int32 objectIndex = 0; objectIndex < this.ExplodedBoundaryObjects.Count; objectIndex++) {
        Rhino.DocObjects.BrepObject boundaryObject = this.ExplodedBoundaryObjects[objectIndex];

        Rhino.Geometry.BoundingBox objectBoundingBox = boundaryObject.Geometry.GetBoundingBox(true);
        Rhino.Geometry.Point3d boundingBoxCenter = objectBoundingBox.Center;

        this.BoundaryCollectionBoundingBox.Union(objectBoundingBox);
        this.BoundaryCollectionCentroids.Add(boundingBoxCenter);
      }

      this.BoundaryCollectionDiagonalSize = this.BoundaryCollectionBoundingBox.Diagonal.Length;
      this.BoundaryCollectionCenter = this.BoundaryCollectionBoundingBox.Center;

      Rhino.RhinoApp.WriteLine("Winder: Defined boundary collection attributes");
    }

    public void FindBoundaryIntegrationMidpoints() {
      this.BoundaryIntegrationMidpoints = new System.Collections.Generic.List<Rhino.Geometry.Point3d>();

      Rhino.Geometry.Vector3d defaultMarginVector = new Rhino.Geometry.Vector3d(
        WinderHandlers.StaticBundles.DefaultOrthogonalMarginSize,
        WinderHandlers.StaticBundles.DefaultOrthogonalMarginSize,
        WinderHandlers.StaticBundles.DefaultOrthogonalMarginSize
      );

      Rhino.Geometry.BoundingBox boundaryCollectionInflatedBoundingBox = new Rhino.Geometry.BoundingBox(
        this.BoundaryCollectionBoundingBox.Min - defaultMarginVector,
        this.BoundaryCollectionBoundingBox.Max + defaultMarginVector
      );

      System.Double inflatedBoundingBoxXLength = boundaryCollectionInflatedBoundingBox.Max.X - boundaryCollectionInflatedBoundingBox.Min.X;
      System.Double inflatedBoundingBoxZLength = boundaryCollectionInflatedBoundingBox.Max.Z - boundaryCollectionInflatedBoundingBox.Min.Z;

      System.Double xIntegrationRaysDistance = inflatedBoundingBoxXLength / (WinderHandlers.StaticBundles.DefaultXIntegrationRaysCount + 1);
      System.Double zIntegrationRaysDistance = inflatedBoundingBoxZLength / (WinderHandlers.StaticBundles.DefaultZIntegrationRaysCount + 1);

      for (System.Int32 zIntegrationRaysCount = 1; zIntegrationRaysCount <= WinderHandlers.StaticBundles.DefaultZIntegrationRaysCount; zIntegrationRaysCount++) {
        for (System.Int32 xIntegrationRaysCount = 1; xIntegrationRaysCount <= WinderHandlers.StaticBundles.DefaultXIntegrationRaysCount; xIntegrationRaysCount++) {
          Rhino.Geometry.Point3d integrationRayStart = new Rhino.Geometry.Point3d(
            boundaryCollectionInflatedBoundingBox.Min.X + xIntegrationRaysDistance * xIntegrationRaysCount,
            boundaryCollectionInflatedBoundingBox.Min.Y,
            boundaryCollectionInflatedBoundingBox.Min.Z + zIntegrationRaysDistance * zIntegrationRaysCount
          );

          Rhino.Geometry.Point3d integrationRayEnd = new Rhino.Geometry.Point3d(
            boundaryCollectionInflatedBoundingBox.Min.X + xIntegrationRaysDistance * xIntegrationRaysCount,
            boundaryCollectionInflatedBoundingBox.Max.Y,
            boundaryCollectionInflatedBoundingBox.Min.Z + zIntegrationRaysDistance * zIntegrationRaysCount
          );

          Rhino.Geometry.Line integrationRayLine = new Rhino.Geometry.Line(integrationRayStart, integrationRayEnd);
          Rhino.Geometry.Curve integrationRayCurve = integrationRayLine.ToNurbsCurve();

          System.Collections.Generic.List<Rhino.Geometry.Point3d> rayImportantPoints = new System.Collections.Generic.List<Rhino.Geometry.Point3d> {
            integrationRayStart,
            integrationRayEnd
          };

          foreach (Rhino.Geometry.Brep joinedBoundary in this.ConnectedBoundaries) {
            Rhino.Geometry.Intersect.Intersection.CurveBrep(
              integrationRayCurve,
              joinedBoundary,
              Rhino.RhinoMath.ZeroTolerance,
              out Rhino.Geometry.Curve[] overlapCurves,
              out Rhino.Geometry.Point3d[] intersectionPoints
            );

            foreach (Rhino.Geometry.Point3d intersectionPoint in intersectionPoints) {
              rayImportantPoints.Add(intersectionPoint);
            }
          }

          System.Collections.Generic.HashSet<Rhino.Geometry.Point3d> rayImportantPointsHashet = new System.Collections.Generic.HashSet<Rhino.Geometry.Point3d>(rayImportantPoints);
          System.Collections.Generic.List<Rhino.Geometry.Point3d> rayUniqueImportantPoints = new System.Collections.Generic.List<Rhino.Geometry.Point3d>(rayImportantPointsHashet);

          if (rayUniqueImportantPoints.Count > 2 && rayUniqueImportantPoints.Count % 2 == 0) {
            System.Collections.Generic.List<Rhino.Geometry.Point3d> rayAlignedUniqueImportantPoints = new System.Collections.Generic.List<Rhino.Geometry.Point3d>(rayUniqueImportantPoints);
            rayAlignedUniqueImportantPoints.Sort((point1, point2) => point1.DistanceTo(integrationRayStart).CompareTo(point2.DistanceTo(integrationRayStart)));

            System.Boolean isNextRaySegmentInside = false;

            for (System.Int32 alignedPointIndex = 0; alignedPointIndex < rayAlignedUniqueImportantPoints.Count - 1; alignedPointIndex++) {
              Rhino.Geometry.Point3d analyzingRayPoint = rayAlignedUniqueImportantPoints[alignedPointIndex];
              Rhino.Geometry.Point3d nextRayPoint = rayAlignedUniqueImportantPoints[alignedPointIndex + 1];

              if (isNextRaySegmentInside) {
                Rhino.Geometry.Line interiorRaySegment = new Rhino.Geometry.Line(analyzingRayPoint, nextRayPoint);

                Rhino.Geometry.Point3d segmentMidpoint = new Rhino.Geometry.Point3d(
                  (analyzingRayPoint.X + nextRayPoint.X) / 2,
                  (analyzingRayPoint.Y + nextRayPoint.Y) / 2,
                  (analyzingRayPoint.Z + nextRayPoint.Z) / 2
                );

                this.BoundaryIntegrationMidpoints.Add(segmentMidpoint);
                Rhino.RhinoDoc.ActiveDoc.Objects.AddLine(interiorRaySegment);
              }

              isNextRaySegmentInside = !isNextRaySegmentInside;
            }
          }
        }
      }
    
      Rhino.RhinoDoc.ActiveDoc.Views.Redraw();

      Rhino.RhinoApp.WriteLine("Winder: Found boundary integration midpoints");
    }
  }

  public class StaticBundles {
    public static System.Int32 CreateOrGetLayerIndex(System.String name, System.Int16 red, System.Int16 green, System.Int16 blue) {
      Rhino.DocObjects.Layer existingLayer = Rhino.RhinoDoc.ActiveDoc.Layers.FindName(name);

      if (existingLayer == null) {
        Rhino.DocObjects.Layer createdLayer = new Rhino.DocObjects.Layer() {
          Name = name,
          Color = System.Drawing.Color.FromArgb(red, green, blue),
          IsVisible = true,
          IsLocked = false
        };

        System.Int32 layerIndex = Rhino.RhinoDoc.ActiveDoc.Layers.Add(createdLayer);
        return layerIndex;
      }

      else {
        System.Int32 layerIndex = existingLayer.Index;
        return layerIndex;
      }
    }

    public static System.Double MaximumDistanceTolerance = 0.001;
    
    public static System.Double DefaultOrthogonalMarginSize = 1;

    public static System.Int32 DefaultXIntegrationRaysCount = 5;
    public static System.Int32 DefaultZIntegrationRaysCount = 100;
  }
}