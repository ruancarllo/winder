namespace WinderWorkers {
  public class ProcessesExecutor {
    private readonly System.Collections.Generic.List<Rhino.DocObjects.BrepObject> ExplodedBoundaryObjects;
    private readonly System.Collections.Generic.List<Rhino.Geometry.Brep> JoinedBoundaryGeometries;

    public ProcessesExecutor() {
      this.ExplodedBoundaryObjects = new System.Collections.Generic.List<Rhino.DocObjects.BrepObject>();
      this.JoinedBoundaryGeometries = new System.Collections.Generic.List<Rhino.Geometry.Brep>();
    }

    public void EvaluateSelectedObjects() {
      System.Collections.Generic.IEnumerable<Rhino.DocObjects.RhinoObject> selectedObjects = Rhino.RhinoDoc.ActiveDoc.Objects.GetSelectedObjects(false, false);
      System.Collections.Generic.List<Rhino.DocObjects.BrepObject> boundaryObjects = new System.Collections.Generic.List<Rhino.DocObjects.BrepObject>();
      System.Collections.Generic.List<Rhino.Geometry.Brep> boundaryGeometries = new System.Collections.Generic.List<Rhino.Geometry.Brep>();

      foreach (Rhino.DocObjects.RhinoObject selectedObject in selectedObjects) {
        if (selectedObject.ObjectType == Rhino.DocObjects.ObjectType.Brep) {
          Rhino.DocObjects.BrepObject boundaryObject = selectedObject as Rhino.DocObjects.BrepObject;
          Rhino.DocObjects.ObjectAttributes boundaryAttributes = boundaryObject.Attributes;
          Rhino.Geometry.Collections.BrepFaceList boundaryFaces = boundaryObject.BrepGeometry.Faces;

          boundaryObjects.Add(boundaryObject);
          boundaryGeometries.Add(boundaryObject.BrepGeometry);
          
          foreach (Rhino.Geometry.BrepFace boundaryFace in boundaryFaces) {
            System.Guid explodedObjectGuid = Rhino.RhinoDoc.ActiveDoc.Objects.Add(boundaryFace, boundaryAttributes);
            Rhino.DocObjects.BrepObject explodedObject = Rhino.RhinoDoc.ActiveDoc.Objects.Find(explodedObjectGuid) as Rhino.DocObjects.BrepObject;

            this.ExplodedBoundaryObjects.Add(explodedObject);
          }

          Rhino.RhinoDoc.ActiveDoc.Objects.Delete(boundaryObject);
        }
      }

      Rhino.RhinoDoc.ActiveDoc.Views.Redraw();

      Rhino.Geometry.Brep[] geometriesJunction = Rhino.Geometry.Brep.JoinBreps(
        boundaryGeometries, Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance
      );

      if (geometriesJunction != null) {
        foreach (Rhino.Geometry.Brep joinedGeometry in geometriesJunction) {
          this.JoinedBoundaryGeometries.Add(joinedGeometry);
        }
      }
    }
  }
}