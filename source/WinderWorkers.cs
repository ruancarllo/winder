namespace WinderWorkers {
  public class ProcessesExecutor {
    private readonly System.Boolean LayeredMode;

    private readonly System.Int32 WrongLayerIndex;
    private readonly System.Int32 CorrectLayerIndex;
    private readonly System.Int32 ModifyedLayerIndex;

    private readonly System.Collections.Generic.List<Rhino.DocObjects.RhinoObject> SelectedObjects;

    private readonly System.Collections.Generic.List<Rhino.DocObjects.BrepObject> BoundaryObjects;

    private readonly System.Collections.Generic.List<Rhino.Geometry.Brep> BoundaryGeometries;

    private readonly System.Collections.Generic.List<Rhino.Geometry.Brep> JoinedGeometries;

    private readonly System.Collections.Generic.Dictionary<System.String, System.Int32> ObjectsFlipPunctuation;
    
    private readonly System.Collections.Generic.List<Rhino.Geometry.Point3d> CorrelatedCenterPoints;

    private readonly System.Collections.Generic.List<Rhino.Geometry.Vector3d> CorrelatedNormalVectors;

    private readonly System.Collections.Generic.List<Rhino.Geometry.Curve> CorrelatedNormalCurves;

    private readonly System.Collections.Generic.List<Rhino.Geometry.Point3d> CorrelatedCurveStarts;
    private readonly System.Collections.Generic.List<Rhino.Geometry.Point3d> CorrelatedCurveEnds;

    private readonly System.Collections.Generic.List<System.Guid> CorrelatedObjectGuids;

    private System.Int32 CorrelatedCount;

    private System.Double BoundaryDiagonalLength;

    private Rhino.Geometry.Point3d BoundaryGlobalCenter;

    public ProcessesExecutor(System.Boolean layeredMode) {
      this.LayeredMode = layeredMode;

      if (this.LayeredMode) {
        WrongLayerIndex = WinderWorkers.ProcessesExecutor.CreateOrGetLayerIndex("Winder Wrong Layer", 255, 0, 0);
        CorrectLayerIndex = WinderWorkers.ProcessesExecutor.CreateOrGetLayerIndex("Winder Correct Layer", 0, 255, 0);
        ModifyedLayerIndex = WinderWorkers.ProcessesExecutor.CreateOrGetLayerIndex("Winder Modifyed Layer", 0, 0, 255);
      }

      this.SelectedObjects = new System.Collections.Generic.List<Rhino.DocObjects.RhinoObject>();

      this.BoundaryObjects = new System.Collections.Generic.List<Rhino.DocObjects.BrepObject>();

      this.BoundaryGeometries = new System.Collections.Generic.List<Rhino.Geometry.Brep>();

      this.JoinedGeometries = new System.Collections.Generic.List<Rhino.Geometry.Brep>();

      this.ObjectsFlipPunctuation = new System.Collections.Generic.Dictionary<System.String, System.Int32>();

      this.CorrelatedCenterPoints = new System.Collections.Generic.List<Rhino.Geometry.Point3d>();
      
      this.CorrelatedNormalVectors = new System.Collections.Generic.List<Rhino.Geometry.Vector3d>();

      this.CorrelatedNormalCurves = new System.Collections.Generic.List<Rhino.Geometry.Curve>();

      this.CorrelatedCurveStarts = new System.Collections.Generic.List<Rhino.Geometry.Point3d>();
      this.CorrelatedCurveEnds = new System.Collections.Generic.List<Rhino.Geometry.Point3d>();

      this.CorrelatedObjectGuids = new System.Collections.Generic.List<System.Guid>();

      this.CorrelatedCount = 0;

      this.BoundaryDiagonalLength = 0.0;

      this.BoundaryGlobalCenter = Rhino.Geometry.Point3d.Origin;
    }

    private static System.Int32 CreateOrGetLayerIndex(System.String name, System.Int16 red, System.Int16 green, System.Int16 blue) {
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

    private static readonly Rhino.Geometry.MeshingParameters MinimalMeshingParameters = new Rhino.Geometry.MeshingParameters(0.0);

    private static readonly System.Double MaximumDistanceTolerance = 0.001;

    private static readonly System.Double DefaultT3Coordinate = 0.0;

    public void SetSeletedObjects() {
      System.Collections.Generic.IEnumerable<Rhino.DocObjects.RhinoObject> objectsSelection = Rhino.RhinoDoc.ActiveDoc.Objects.GetSelectedObjects(false, false);

      foreach (Rhino.DocObjects.RhinoObject selectedObject in objectsSelection) {
        this.SelectedObjects.Add(selectedObject);
      } 
    }

    public void FilterBoundaryObjects() {
      foreach (Rhino.DocObjects.RhinoObject selectedObject in this.SelectedObjects) {
        if (selectedObject.ObjectType == Rhino.DocObjects.ObjectType.Brep) {
          this.BoundaryObjects.Add(selectedObject as Rhino.DocObjects.BrepObject);
        }
      }
    }

    public void PickBoundaryGeometries() {
      foreach (Rhino.DocObjects.BrepObject boundaryObject in this.BoundaryObjects) {
        this.BoundaryGeometries.Add(boundaryObject.BrepGeometry);
      }
    }

    public void ProcessJoinedGeometries() {
      Rhino.Geometry.Brep[] geometriesJunction = Rhino.Geometry.Brep.JoinBreps(
        this.BoundaryGeometries, Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance
      );

      if (geometriesJunction != null) {
        foreach (Rhino.Geometry.Brep joinedGeometry in geometriesJunction) {
          this.JoinedGeometries.Add(joinedGeometry);
        }
      }
    }

    public void CalculateBoundaryMathematics() {
      Rhino.Geometry.BoundingBox boundaryBoundingBox = Rhino.Geometry.BoundingBox.Empty;

      foreach (Rhino.DocObjects.BrepObject boundaryObject in this.BoundaryObjects) {
        Rhino.Geometry.BoundingBox objectBoundingBox = boundaryObject.Geometry.GetBoundingBox(true);
        boundaryBoundingBox.Union(objectBoundingBox);
      }

      this.BoundaryDiagonalLength = boundaryBoundingBox.Diagonal.Length;

      this.BoundaryGlobalCenter = boundaryBoundingBox.Center;
    }

    public void InitializeCorrelatedParameters() {
      foreach (Rhino.DocObjects.BrepObject boundaryObject in this.BoundaryObjects) {
        System.Guid objectGuid = boundaryObject.Id;

        Rhino.Geometry.Mesh[] objectMeshes = Rhino.Geometry.Mesh.CreateFromBrep(boundaryObject.BrepGeometry, WinderWorkers.ProcessesExecutor.MinimalMeshingParameters);

        foreach (Rhino.Geometry.Mesh objectMesh in objectMeshes) {
          for (System.Int32 faceIndex = 0; faceIndex < objectMesh.Faces.Count; faceIndex++) {
            Rhino.Geometry.Point3d centerPoint = objectMesh.Faces.GetFaceCenter(faceIndex);

            Rhino.Geometry.Vector3d normalVector = objectMesh.NormalAt(faceIndex, centerPoint.X, centerPoint.Y, centerPoint.Z, WinderWorkers.ProcessesExecutor.DefaultT3Coordinate);

            Rhino.Geometry.Line negativeSegment = new Rhino.Geometry.Line(centerPoint, normalVector, -this.BoundaryDiagonalLength);
            Rhino.Geometry.Line positiveSegment = new Rhino.Geometry.Line(centerPoint, normalVector, +this.BoundaryDiagonalLength);

            Rhino.Geometry.Point3d lineMin = new Rhino.Geometry.Point3d(negativeSegment.ToX, negativeSegment.ToY, negativeSegment.ToZ);
            Rhino.Geometry.Point3d lineMax = new Rhino.Geometry.Point3d(positiveSegment.ToX, positiveSegment.ToY, positiveSegment.ToZ);

            Rhino.Geometry.Line normalLine = new Rhino.Geometry.Line(lineMin, lineMax);
            Rhino.Geometry.Curve normalCurve = normalLine.ToNurbsCurve();

            this.CorrelatedCenterPoints.Add(centerPoint);

            this.CorrelatedNormalVectors.Add(normalVector);

            this.CorrelatedNormalCurves.Add(normalCurve);

            this.CorrelatedCurveStarts.Add(lineMin);
            this.CorrelatedCurveEnds.Add(lineMax);

            this.CorrelatedObjectGuids.Add(objectGuid);

            this.CorrelatedCount++;
          }
        }
      }

      for (System.Int32 correlatedIndex = 0; correlatedIndex < this.CorrelatedCount; correlatedIndex++) {
        System.Guid objectGuid = this.CorrelatedObjectGuids[correlatedIndex];
        this.ObjectsFlipPunctuation[objectGuid.ToString()] = 0;
      }
    }
  
    public void CalculateFlipPunctuations() {
      for (System.Int32 correlatedIndex = 0; correlatedIndex < this.CorrelatedCount; correlatedIndex++) {
        Rhino.Geometry.Point3d centerPoint = this.CorrelatedCenterPoints[correlatedIndex];

        Rhino.Geometry.Vector3d normalVector = this.CorrelatedNormalVectors[correlatedIndex];

        Rhino.Geometry.Curve normalCurve = this.CorrelatedNormalCurves[correlatedIndex];

        Rhino.Geometry.Point3d curveStart = this.CorrelatedCurveStarts[correlatedIndex];
        Rhino.Geometry.Point3d curveEnd = this.CorrelatedCurveEnds[CorrectLayerIndex];

        System.Guid objectGuid = this.CorrelatedObjectGuids[correlatedIndex];

        System.Collections.Generic.List<Rhino.Geometry.Point3d> importantPoints = new System.Collections.Generic.List<Rhino.Geometry.Point3d> {
          curveStart,
          curveEnd
        };

        foreach (Rhino.Geometry.Brep joinedGeometry in this.JoinedGeometries) {
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

        if (importantPoints.Count % 2 == 0) {
          System.Collections.Generic.List<Rhino.Geometry.Point3d> alignedPoints = new System.Collections.Generic.List<Rhino.Geometry.Point3d>(importantPoints);
          alignedPoints.Sort((Rhino.Geometry.Point3d point1, Rhino.Geometry.Point3d point2) => point1.DistanceTo(curveStart).CompareTo(point2.DistanceTo(curveStart)));

          System.Boolean isNextSegmentInside = false;

          for (System.Int32 alignedPointIndex = 0; alignedPointIndex < alignedPoints.Count - 1; alignedPointIndex++) {
            Rhino.Geometry.Point3d analyzingPoint = alignedPoints[alignedPointIndex];
            
            if (analyzingPoint.DistanceTo(centerPoint) < WinderWorkers.ProcessesExecutor.MaximumDistanceTolerance) {
              System.Double dotProduct = 0.0;

              if (isNextSegmentInside) {
                Rhino.Geometry.Point3d posteriorPoint = alignedPoints[alignedPointIndex + 1];

                Rhino.Geometry.Vector3d desiredVector = new Rhino.Geometry.Vector3d(
                  posteriorPoint.X - analyzingPoint.X,
                  posteriorPoint.Y - analyzingPoint.Y,
                  posteriorPoint.Z - analyzingPoint.Z
                );

                dotProduct = normalVector * desiredVector;
              }

              else {
                Rhino.Geometry.Point3d anteriorPoint = alignedPoints[alignedPointIndex - 1];

                Rhino.Geometry.Vector3d desiredVector = new Rhino.Geometry.Vector3d(
                  anteriorPoint.X - analyzingPoint.X,
                  anteriorPoint.Y - analyzingPoint.Y,
                  anteriorPoint.Z - analyzingPoint.Z
                );

                dotProduct = normalVector * desiredVector;
              }

              if (dotProduct > 0) {
                this.ObjectsFlipPunctuation[objectGuid.ToString()] += 1;
              }

              if (dotProduct < 0) {
                this.ObjectsFlipPunctuation[objectGuid.ToString()] -= 1;
              }
            }
          
            isNextSegmentInside = !isNextSegmentInside;
          }
        }
      
        else {
          Rhino.Geometry.Vector3d centerVector = new Rhino.Geometry.Vector3d(
            centerPoint.X - this.BoundaryGlobalCenter.X,
            centerPoint.Y - this.BoundaryGlobalCenter.Y,
            centerPoint.Z - this.BoundaryGlobalCenter.Z
          );

          normalVector.Unitize();

          Rhino.Geometry.Vector3d summationVector = centerVector + normalVector;
          Rhino.Geometry.Vector3d subtractionVector = centerVector - normalVector;

          if (summationVector.Length > subtractionVector.Length) {
            this.ObjectsFlipPunctuation[objectGuid.ToString()] -= 1;
          }

          if (summationVector.Length < subtractionVector.Length) {
            this.ObjectsFlipPunctuation[objectGuid.ToString()] += 1;
          }
        }
      }
    }
  
    public void HarmonizeBoundaryNormals() {
      foreach (System.Collections.Generic.KeyValuePair<System.String, System.Int32> keyValuePair in this.ObjectsFlipPunctuation) {
        System.Guid objectGuid = System.Guid.Parse(keyValuePair.Key);
        System.Int32 flipPunctuation = ObjectsFlipPunctuation[keyValuePair.Key];

        Rhino.DocObjects.BrepObject analyzingObject = Rhino.RhinoDoc.ActiveDoc.Objects.Find(objectGuid) as Rhino.DocObjects.BrepObject;

        Rhino.DocObjects.ObjectAttributes newAttributes = analyzingObject.Attributes;
        Rhino.Geometry.Brep newGeometry = analyzingObject.BrepGeometry;

        if (flipPunctuation > 0) {
          newGeometry.Flip();
        }

        if (this.LayeredMode) {
          if (flipPunctuation > 0) newAttributes.LayerIndex = this.ModifyedLayerIndex;
          if (flipPunctuation < 0) newAttributes.LayerIndex = this.CorrectLayerIndex;
          if (flipPunctuation == 0) newAttributes.LayerIndex = this.WrongLayerIndex;
        }

        Rhino.RhinoDoc.ActiveDoc.Objects.Delete(analyzingObject);
        Rhino.RhinoDoc.ActiveDoc.Objects.Add(newGeometry, newAttributes);
      }
    }
  }
}