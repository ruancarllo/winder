namespace WinderWorkers {
  public class ProcessesExecutor {
    private readonly System.Boolean VerboseMode;
    private readonly System.Boolean LayeredMode;
    private readonly System.Boolean InteractiveMode;

    private readonly System.Collections.Generic.List<Rhino.DocObjects.RhinoObject> SelectedObjects;
    private readonly System.Collections.Generic.List<Rhino.DocObjects.BrepObject> BoundaryObjects;
    private readonly System.Collections.Generic.List<Rhino.Geometry.Brep> BoundaryGeometries;
    private readonly System.Collections.Generic.List<Rhino.Geometry.Brep> JoinedGeometries;

    private Rhino.Geometry.Point3d BoundaryGlobalCenter;
    private System.Double BoundaryDiagonalLength;

    private readonly System.Int32 WrongLayerIndex;
    private readonly System.Int32 CorrectLayerIndex;
    private readonly System.Int32 ModifyedLayerIndex;
    private readonly System.Int32 InteractiveLayerIndex;

    private static readonly Rhino.Geometry.MeshingParameters MinimalMeshingParameters = new Rhino.Geometry.MeshingParameters(0.0);
    private static readonly System.Double MaximumDistanceTolerance = 0.001;
    private static readonly System.Double DefaultT3Coordinate = 0.0;

    public ProcessesExecutor(System.Boolean verboseMode, System.Boolean layeredMode, System.Boolean interactiveMode) {
      this.VerboseMode = verboseMode;
      this.LayeredMode = layeredMode;
      this.InteractiveMode = interactiveMode;

      this.SelectedObjects = new System.Collections.Generic.List<Rhino.DocObjects.RhinoObject>();
      this.BoundaryObjects = new System.Collections.Generic.List<Rhino.DocObjects.BrepObject>();
      this.BoundaryGeometries = new System.Collections.Generic.List<Rhino.Geometry.Brep>();
      this.JoinedGeometries = new System.Collections.Generic.List<Rhino.Geometry.Brep>();

      this.BoundaryDiagonalLength = 0.0;
      this.BoundaryGlobalCenter = Rhino.Geometry.Point3d.Unset;

      if (this.LayeredMode) {
        this.WrongLayerIndex = WinderWorkers.ProcessesExecutor.CreateOrGetLayerIndex("Winder Wrong Layer", 255, 0, 0);
        this.CorrectLayerIndex = WinderWorkers.ProcessesExecutor.CreateOrGetLayerIndex("Winder Correct Layer", 0, 255, 0);
        this.ModifyedLayerIndex = WinderWorkers.ProcessesExecutor.CreateOrGetLayerIndex("Winder Modifyed Layer", 0, 0, 255);
      }

      if (this.InteractiveMode) {
        this.InteractiveLayerIndex = WinderWorkers.ProcessesExecutor.CreateOrGetLayerIndex("Winder Interactive Layer", 0, 255, 255);
      }
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

    public void SetSeletedObjects() {
      System.Collections.Generic.IEnumerable<Rhino.DocObjects.RhinoObject> objectsSelection = Rhino.RhinoDoc.ActiveDoc.Objects.GetSelectedObjects(false, false);

      foreach (Rhino.DocObjects.RhinoObject selectedObject in objectsSelection) {
        this.SelectedObjects.Add(selectedObject);
      }

      if (this.VerboseMode) {
        Rhino.RhinoApp.WriteLine($"Winder: Setted {this.SelectedObjects.Count} selected objects");
      }
    }

    public void SetBoundaryObjects() {
      foreach (Rhino.DocObjects.RhinoObject selectedObject in this.SelectedObjects) {
        if (selectedObject.ObjectType == Rhino.DocObjects.ObjectType.Brep) {
          this.BoundaryObjects.Add(selectedObject as Rhino.DocObjects.BrepObject);
        }
      }

      if (this.VerboseMode) {
        Rhino.RhinoApp.WriteLine($"Winder: Setted {this.BoundaryObjects.Count} boundary objects");
      }
    }

    public void SetBoundaryGeometries() {
      foreach (Rhino.DocObjects.BrepObject boundaryObject in this.BoundaryObjects) {
        this.BoundaryGeometries.Add(boundaryObject.BrepGeometry);
      }

      if (this.VerboseMode) {
        Rhino.RhinoApp.WriteLine($"Winder: Setted {this.BoundaryGeometries.Count} boundary geometries");
      }
    }

    public void SetJoinedGeometries() {
      Rhino.Geometry.Brep[] geometriesJunction = Rhino.Geometry.Brep.JoinBreps(
        this.BoundaryGeometries, Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance
      );

      if (geometriesJunction != null) {
        foreach (Rhino.Geometry.Brep joinedGeometry in geometriesJunction) {
          this.JoinedGeometries.Add(joinedGeometry);
        }
      }

      if (this.VerboseMode) {
        Rhino.RhinoApp.WriteLine($"Winder: Setted {this.JoinedGeometries.Count} joined geometries");
      }
    }

    public void SetBoundaryMathematics() {
      Rhino.Geometry.BoundingBox boundaryBoundingBox = Rhino.Geometry.BoundingBox.Empty;

      foreach (Rhino.DocObjects.BrepObject boundaryObject in this.BoundaryObjects) {
        Rhino.Geometry.BoundingBox objectBoundingBox = boundaryObject.Geometry.GetBoundingBox(true);
        boundaryBoundingBox.Union(objectBoundingBox);
      }

      this.BoundaryDiagonalLength = boundaryBoundingBox.Diagonal.Length;
      this.BoundaryGlobalCenter = boundaryBoundingBox.Center;

      if (this.VerboseMode) {
        Rhino.RhinoApp.WriteLine($"Winder: Setted boundary objects diagonal length as {this.BoundaryDiagonalLength}");
        Rhino.RhinoApp.WriteLine($"Winder: Setted boundary objects global center as {this.BoundaryGlobalCenter}");
      }
    }

    public void HarmonizeBoundaryNormals() {
      for (System.Int32 boundaryObjectIndex = 0; boundaryObjectIndex < this.BoundaryObjects.Count; boundaryObjectIndex++) {
        Rhino.DocObjects.BrepObject boundaryObject = this.BoundaryObjects[boundaryObjectIndex];

        Rhino.Geometry.Mesh[] objectMeshes = Rhino.Geometry.Mesh.CreateFromBrep(
          boundaryObject.BrepGeometry,
          WinderWorkers.ProcessesExecutor.MinimalMeshingParameters
        );

        System.Collections.Generic.List<System.Guid> normalCurveGuids = new System.Collections.Generic.List<System.Guid>();

        System.Int32 objectFlipPunctuation = 0;

        foreach (Rhino.Geometry.Mesh objectMesh in objectMeshes) {
          System.Int32 facesCount = objectMesh.Faces.Count;

          for (System.Int32 faceIndex = 0; faceIndex < facesCount; faceIndex++) {
            Rhino.Geometry.Point3d centerPoint = objectMesh.Faces.GetFaceCenter(faceIndex);

            Rhino.Geometry.Vector3d normalVector = objectMesh.NormalAt(
              faceIndex,
              centerPoint.X,
              centerPoint.Y,
              centerPoint.Z,
              WinderWorkers.ProcessesExecutor.DefaultT3Coordinate
            );

            Rhino.Geometry.Line positiveNormalSegment = new Rhino.Geometry.Line(centerPoint, normalVector, +this.BoundaryDiagonalLength);
            Rhino.Geometry.Line negativeNormalSegment = new Rhino.Geometry.Line(centerPoint, normalVector, -this.BoundaryDiagonalLength);

            Rhino.Geometry.Point3d lineMax = new Rhino.Geometry.Point3d(positiveNormalSegment.ToX, positiveNormalSegment.ToY, positiveNormalSegment.ToZ);
            Rhino.Geometry.Point3d lineMin = new Rhino.Geometry.Point3d(negativeNormalSegment.ToX, negativeNormalSegment.ToY, negativeNormalSegment.ToZ);

            Rhino.Geometry.Line normalLine = new Rhino.Geometry.Line(lineMin, lineMax);
            Rhino.Geometry.Curve normalCurve = normalLine.ToNurbsCurve();
            
            if (this.InteractiveMode) {
              Rhino.DocObjects.ObjectAttributes curveAttributes = new Rhino.DocObjects.ObjectAttributes {
                LayerIndex = this.InteractiveLayerIndex
              };

              System.Guid normalCurveGuid = Rhino.RhinoDoc.ActiveDoc.Objects.AddCurve(normalCurve, curveAttributes);

              Rhino.RhinoDoc.ActiveDoc.Views.Redraw();

              normalCurveGuids.Add(normalCurveGuid);
            }

            System.Collections.Generic.List<Rhino.Geometry.Point3d> importantPoints = new System.Collections.Generic.List<Rhino.Geometry.Point3d> {
              lineMin,
              lineMax
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
              
              alignedPoints.Sort(
                (Rhino.Geometry.Point3d point1, Rhino.Geometry.Point3d point2) => point1.DistanceTo(lineMin).CompareTo(point2.DistanceTo(lineMin))
              );

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
                    objectFlipPunctuation += 1;
                  }

                  if (dotProduct < 0) {
                    objectFlipPunctuation -= 1;
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
                objectFlipPunctuation -= 1;
              }

              if (summationVector.Length < subtractionVector.Length) {
                objectFlipPunctuation += 1;
              }
            }     
          }
        }
      
        Rhino.DocObjects.ObjectAttributes newAttributes = boundaryObject.Attributes;
        Rhino.Geometry.Brep newGeometry = boundaryObject.BrepGeometry;

        if (objectFlipPunctuation > 0) {
          newGeometry.Flip();

          if (this.VerboseMode) {
            Rhino.RhinoApp.WriteLine($"Winder: Flipped boundary object geometry of id {boundaryObject.Id}");
          }
        }

        if (this.LayeredMode) {
          if (objectFlipPunctuation > 0) {
            newAttributes.LayerIndex = this.ModifyedLayerIndex;
          }

          if (objectFlipPunctuation < 0) {
            newAttributes.LayerIndex = this.CorrectLayerIndex;
          }

          if (objectFlipPunctuation == 0) {
            newAttributes.LayerIndex = this.WrongLayerIndex;
          }
        }

        Rhino.RhinoDoc.ActiveDoc.Objects.Delete(boundaryObject);
        Rhino.RhinoDoc.ActiveDoc.Objects.Add(newGeometry, newAttributes);

        if (this.VerboseMode) {
          Rhino.RhinoApp.WriteLine($"Winder: Analyzed boundary object {boundaryObjectIndex + 1} of {this.BoundaryObjects.Count}");
        }

        if (this.InteractiveMode) {
          foreach (System.Guid normalCurveGuid in normalCurveGuids) {
            Rhino.RhinoDoc.ActiveDoc.Objects.Delete(normalCurveGuid, true);
            Rhino.RhinoDoc.ActiveDoc.Views.Redraw();
          }
        }
      }
    }
  }
}