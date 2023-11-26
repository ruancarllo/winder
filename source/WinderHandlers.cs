namespace WinderHandlers {
  public class ProcessesExecutor {
    System.Collections.Generic.List<Rhino.DocObjects.RhinoObject> ExplodedSelectedObjects;
    System.Collections.Generic.List<Rhino.DocObjects.BrepObject> ExplodedBoundaryObjects;

    System.Collections.Generic.List<Rhino.Geometry.Brep> FragmentedBoundaries;
    System.Collections.Generic.List<Rhino.Geometry.Brep> ConnectedBoundaries;

    System.Collections.Generic.List<Rhino.Geometry.Point3d> BoundaryCollectionBoundingBoxCentroids;
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
    }

    public void DefineEssentialLayers() {
      this.InteractiveLayerIndex = WinderHandlers.StaticBundles.CreateOrGetLayerIndex("Winder Interactive Layer", 0, 255, 255);
      this.UnhandledLayerIndex = WinderHandlers.StaticBundles.CreateOrGetLayerIndex("Winder Unhandled Layer", 255, 255, 255);
      this.UndefinedLayerIndex = WinderHandlers.StaticBundles.CreateOrGetLayerIndex("Winder Undefined Layer", 255, 0, 0);
      this.UnflippedLayerIndex = WinderHandlers.StaticBundles.CreateOrGetLayerIndex("Winder Unflipped Layer", 0, 255, 0);
      this.DeductedLayerIndex = WinderHandlers.StaticBundles.CreateOrGetLayerIndex("Winder Deducted Layer", 255, 16, 240);
      this.FlippedLayerIndex = WinderHandlers.StaticBundles.CreateOrGetLayerIndex("Winder Flipped Layer", 0, 0, 255);
    }

    public void DefineInteractiveAttributes() {
      this.InteractiveAttributes = new Rhino.DocObjects.ObjectAttributes {
        LayerIndex = this.InteractiveLayerIndex
      };
    }

    public void RegisterExplodedSelectedObjects() {
      System.Collections.Generic.IEnumerable<Rhino.DocObjects.RhinoObject> objectsSelection = Rhino.RhinoDoc.ActiveDoc.Objects.GetSelectedObjects(false, false);
      this.ExplodedSelectedObjects = new System.Collections.Generic.List<Rhino.DocObjects.RhinoObject>(objectsSelection);
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
    }

    public void FilterExplodedBoundaryObjects() {
      this.ExplodedBoundaryObjects = new System.Collections.Generic.List<Rhino.DocObjects.BrepObject>();

      foreach (Rhino.DocObjects.RhinoObject explodedObject in this.ExplodedSelectedObjects) {
        if (explodedObject.ObjectType == Rhino.DocObjects.ObjectType.Brep) {
          Rhino.DocObjects.BrepObject boundaryObject = explodedObject as Rhino.DocObjects.BrepObject;
          this.ExplodedBoundaryObjects.Add(boundaryObject);
        }
      }
    }

    public void SetFragmentedBoundaries() {
      this.FragmentedBoundaries = new System.Collections.Generic.List<Rhino.Geometry.Brep>();

      foreach (Rhino.DocObjects.BrepObject boundaryObject in this.ExplodedBoundaryObjects) {
        Rhino.Geometry.Brep explodedBoundary = boundaryObject.BrepGeometry;
        this.FragmentedBoundaries.Add(explodedBoundary);
      }
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
    }

    public void DefineBoundaryCollectionAttributes() {
      this.BoundaryCollectionBoundingBox = Rhino.Geometry.BoundingBox.Empty;
      this.BoundaryCollectionBoundingBoxCentroids = new System.Collections.Generic.List<Rhino.Geometry.Point3d>();

      for (System.Int32 objectIndex = 0; objectIndex < this.ExplodedBoundaryObjects.Count; objectIndex++) {
        Rhino.DocObjects.BrepObject boundaryObject = this.ExplodedBoundaryObjects[objectIndex];

        Rhino.Geometry.BoundingBox objectBoundingBox = boundaryObject.Geometry.GetBoundingBox(true);
        Rhino.Geometry.Point3d boundingBoxCentroid = objectBoundingBox.Center;

        this.BoundaryCollectionBoundingBox.Union(objectBoundingBox);
        this.BoundaryCollectionBoundingBoxCentroids.Add(boundingBoxCentroid);
      }

      this.BoundaryCollectionDiagonalSize = this.BoundaryCollectionBoundingBox.Diagonal.Length;
      this.BoundaryCollectionCenter = this.BoundaryCollectionBoundingBox.Center;
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

      System.Double integrationRaysXDistance = inflatedBoundingBoxXLength / (WinderHandlers.StaticBundles.DefaultXIntegrationRaysCount + 1);
      System.Double integrationRaysZDistance = inflatedBoundingBoxZLength / (WinderHandlers.StaticBundles.DefaultZIntegrationRaysCount + 1);

      for (System.Int32 zIntegrationRaysCount = 1; zIntegrationRaysCount <= WinderHandlers.StaticBundles.DefaultZIntegrationRaysCount; zIntegrationRaysCount++) {
        for (System.Int32 xIntegrationRaysCount = 1; xIntegrationRaysCount <= WinderHandlers.StaticBundles.DefaultXIntegrationRaysCount; xIntegrationRaysCount++) {
          Rhino.Geometry.Point3d integrationRayStart = new Rhino.Geometry.Point3d(
            boundaryCollectionInflatedBoundingBox.Min.X + integrationRaysXDistance * xIntegrationRaysCount,
            boundaryCollectionInflatedBoundingBox.Min.Y,
            boundaryCollectionInflatedBoundingBox.Min.Z + integrationRaysZDistance * zIntegrationRaysCount
          );

          Rhino.Geometry.Point3d integrationRayEnd = new Rhino.Geometry.Point3d(
            boundaryCollectionInflatedBoundingBox.Min.X + integrationRaysXDistance * xIntegrationRaysCount,
            boundaryCollectionInflatedBoundingBox.Max.Y,
            boundaryCollectionInflatedBoundingBox.Min.Z + integrationRaysZDistance * zIntegrationRaysCount
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
            
            rayAlignedUniqueImportantPoints.Sort(
              (Rhino.Geometry.Point3d point1, Rhino.Geometry.Point3d point2) => {
                return point1.DistanceTo(integrationRayStart).CompareTo(point2.DistanceTo(integrationRayStart));
              }
            );

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
              }

              isNextRaySegmentInside = !isNextRaySegmentInside;
            }
          }
        }
      }
    }
  
    public void HarmonizeExplodedBoundaryObjectsNormals() {
      for (System.Int32 objectIndex = 0; objectIndex < this.ExplodedBoundaryObjects.Count; objectIndex++) {
        Rhino.DocObjects.BrepObject boundaryObject = this.ExplodedBoundaryObjects[objectIndex];

        Rhino.Geometry.BrepFace boundaryFace = boundaryObject.BrepGeometry.Faces[0];
        Rhino.Geometry.Point3d boundingBoxCentroid = this.BoundaryCollectionBoundingBoxCentroids[objectIndex];

        Rhino.DocObjects.ObjectAttributes newAttributes = boundaryObject.Attributes;
        Rhino.Geometry.Brep newGeometry = boundaryObject.BrepGeometry;

        Rhino.Geometry.Point3d boundaryCentroid = boundaryObject.BrepGeometry.ClosestPoint(boundingBoxCentroid);
        System.Boolean wasBoundaryCentroidFound = boundaryFace.ClosestPoint(boundingBoxCentroid, out double boundaryCentroidU, out double boundaryCentroidV);

        if (boundaryCentroid == Rhino.Geometry.Point3d.Unset || wasBoundaryCentroidFound == false) {
          newAttributes.LayerIndex = this.UndefinedLayerIndex;

          Rhino.RhinoDoc.ActiveDoc.Objects.Add(newGeometry, newAttributes);
          Rhino.RhinoDoc.ActiveDoc.Objects.Delete(boundaryObject);
          Rhino.RhinoDoc.ActiveDoc.Views.Redraw();

          continue;
        }

        Rhino.Geometry.Vector3d boundaryCentroidNormalVector = boundaryFace.NormalAt(boundaryCentroidU, boundaryCentroidV);

        Rhino.Geometry.Line positiveNormalSegment = new Rhino.Geometry.Line(boundaryCentroid, boundaryCentroidNormalVector, +this.BoundaryCollectionDiagonalSize);
        Rhino.Geometry.Line negativeNormalSegment = new Rhino.Geometry.Line(boundaryCentroid, boundaryCentroidNormalVector, -this.BoundaryCollectionDiagonalSize);

        Rhino.Geometry.Point3d normalLineMax = new Rhino.Geometry.Point3d(positiveNormalSegment.ToX, positiveNormalSegment.ToY, positiveNormalSegment.ToZ);
        Rhino.Geometry.Point3d normalLineMin = new Rhino.Geometry.Point3d(negativeNormalSegment.ToX, negativeNormalSegment.ToY, negativeNormalSegment.ToZ);

        Rhino.Geometry.Line normalLine = new Rhino.Geometry.Line(normalLineMin, normalLineMax);
        Rhino.Geometry.Curve normalCurve = normalLine.ToNurbsCurve();

        System.Guid normalCurveGuid = Rhino.RhinoDoc.ActiveDoc.Objects.AddCurve(normalCurve, this.InteractiveAttributes);
        Rhino.RhinoDoc.ActiveDoc.Views.Redraw();

        System.Collections.Generic.List<Rhino.Geometry.Point3d> normalImportantPoints = new System.Collections.Generic.List<Rhino.Geometry.Point3d> {normalLineMin, normalLineMax};

        foreach (Rhino.Geometry.Brep joinedBoundary in this.ConnectedBoundaries) {
          Rhino.Geometry.Intersect.Intersection.CurveBrep(
            normalCurve,
            joinedBoundary,
            Rhino.RhinoMath.ZeroTolerance,
            out Rhino.Geometry.Curve[] overlapCurves,
            out Rhino.Geometry.Point3d[] intersectionPoints
          );

          foreach (Rhino.Geometry.Point3d intersectionPoint in intersectionPoints) {
            normalImportantPoints.Add(intersectionPoint);
          }
        }

        System.Collections.Generic.HashSet<Rhino.Geometry.Point3d> normalImportantPointsHashet = new System.Collections.Generic.HashSet<Rhino.Geometry.Point3d>(normalImportantPoints);
        System.Collections.Generic.List<Rhino.Geometry.Point3d> normalUniqueImportantPoints = new System.Collections.Generic.List<Rhino.Geometry.Point3d>(normalImportantPointsHashet);

        if (normalUniqueImportantPoints.Count > 2 && normalUniqueImportantPoints.Count % 2 == 0) {
          System.Collections.Generic.List<Rhino.Geometry.Point3d> alignedPoints = new System.Collections.Generic.List<Rhino.Geometry.Point3d>(normalUniqueImportantPoints);
          
          alignedPoints.Sort(
            (Rhino.Geometry.Point3d point1, Rhino.Geometry.Point3d point2) => {
              return point1.DistanceTo(normalLineMin).CompareTo(point2.DistanceTo(normalLineMin));
            }
          );

          System.Boolean isNextSegmentInside = false;

          for (System.Int32 pointIndex = 0; pointIndex < alignedPoints.Count - 1; pointIndex++) {
            Rhino.Geometry.Point3d analyzingNormalPoint = alignedPoints[pointIndex];

            if (analyzingNormalPoint.DistanceTo(boundaryCentroid) < WinderHandlers.StaticBundles.MaximumDistanceTolerance) {
              System.Int32 comparativeDirectionIndex = isNextSegmentInside ? +1 : -1;
              Rhino.Geometry.Point3d comparativeNormalPoint = alignedPoints[pointIndex + comparativeDirectionIndex];

              Rhino.Geometry.Vector3d desiredNormalVector = new Rhino.Geometry.Vector3d(
                comparativeNormalPoint.X - analyzingNormalPoint.X,
                comparativeNormalPoint.Y - analyzingNormalPoint.Y,
                comparativeNormalPoint.Z - analyzingNormalPoint.Z
              );

              System.Double vectorsDotProduct = boundaryCentroidNormalVector * desiredNormalVector;

              if (vectorsDotProduct > 0) {
                newGeometry.Flip();
                newAttributes.LayerIndex = this.FlippedLayerIndex;
              }

              if (vectorsDotProduct < 0) {
                newAttributes.LayerIndex = this.UnflippedLayerIndex;
              }

              if (vectorsDotProduct == 0) {
                newAttributes.LayerIndex = this.UndefinedLayerIndex;
              }

              Rhino.RhinoDoc.ActiveDoc.Objects.Delete(boundaryObject);
              Rhino.RhinoDoc.ActiveDoc.Objects.Add(newGeometry, newAttributes);
              Rhino.RhinoDoc.ActiveDoc.Views.Redraw();
            }

            isNextSegmentInside = !isNextSegmentInside;
          }
        }
      
        if (normalUniqueImportantPoints.Count > 2 && normalUniqueImportantPoints.Count % 2 != 0) {
          Rhino.Geometry.Vector3d centerCentroidVector = new Rhino.Geometry.Vector3d(
            boundaryCentroid.X - this.BoundaryCollectionCenter.X,
            boundaryCentroid.Y - this.BoundaryCollectionCenter.Y,
            boundaryCentroid.Z - this.BoundaryCollectionCenter.Z
          );

          boundaryCentroidNormalVector.Unitize();

          Rhino.Geometry.Vector3d summationVector = centerCentroidVector + boundaryCentroidNormalVector;
          Rhino.Geometry.Vector3d subtractionVector = centerCentroidVector - boundaryCentroidNormalVector;

          if (summationVector.Length < subtractionVector.Length) {
            newGeometry.Flip();
            newAttributes.LayerIndex = this.DeductedLayerIndex;
          }

          if (summationVector.Length > subtractionVector.Length) {
            newAttributes.LayerIndex = this.DeductedLayerIndex;
          }

          if (summationVector.Length == subtractionVector.Length) {
            newAttributes.LayerIndex = this.UndefinedLayerIndex;
          }

          Rhino.RhinoDoc.ActiveDoc.Objects.Add(newGeometry, newAttributes);
          Rhino.RhinoDoc.ActiveDoc.Objects.Delete(boundaryObject);
          Rhino.RhinoDoc.ActiveDoc.Views.Redraw();
        }

        if (normalUniqueImportantPoints.Count <= 2) {
          newAttributes.LayerIndex = this.UndefinedLayerIndex;

          Rhino.RhinoDoc.ActiveDoc.Objects.Add(newGeometry, newAttributes);
          Rhino.RhinoDoc.ActiveDoc.Objects.Delete(boundaryObject);
          Rhino.RhinoDoc.ActiveDoc.Views.Redraw();
        }

        Rhino.RhinoDoc.ActiveDoc.Objects.Delete(normalCurveGuid, true);
        Rhino.RhinoDoc.ActiveDoc.Views.Redraw();
      }
    }

    public void DeleteInteractiveLayer() {
      Rhino.RhinoDoc.ActiveDoc.Layers.Delete(this.InteractiveLayerIndex, true);
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

    public static System.Int32 DefaultXIntegrationRaysCount = 100;
    public static System.Int32 DefaultZIntegrationRaysCount = 100;
  }
}