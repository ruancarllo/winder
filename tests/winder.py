import System
import Rhino

class Winder:
  MINIMAL_MESHING_PARAMETERS = Rhino.Geometry.MeshingParameters(0)

  def __init__(self, layaered_mode):
    self.layered_mode = layaered_mode

    if layaered_mode == True:
      self.wrong_layer_index = Winder.create_layer_index("Wrong Winder Layer", 255, 0, 0)
      self.correct_layer_index = Winder.create_layer_index("Correct Winder Layer", 0, 255, 0)
      self.modifyed_layer_index = Winder.create_layer_index("Modifyed Winder Layer", 0, 0, 255)

    self.objects_flip_puntuaction = {}

    self.correlated_normal_vectors = []
    self.correlated_center_points = []

    self.correlated_vector_curves = []
    self.correlated_curve_starts = []
    self.correlated_curve_ends = []

    self.correlated_object_ids = []

    self.correlated_count = 0

  def set_selected_objects(self):
    self.selected_objects = System.Collections.Generic.List[Rhino.DocObjects.RhinoObject](
      Rhino.RhinoDoc.ActiveDoc.Objects.GetSelectedObjects(False, False)
    )
  
  def filter_boundary_objects(self):
    self.boundary_objects = [
      selected_object for selected_object in self.selected_objects if selected_object.ObjectType == Rhino.DocObjects.ObjectType.Brep
    ]
  
  def pick_boundary_geometries(self):
    self.boundary_geometries = [
      boundary_object.BrepGeometry for boundary_object in self.boundary_objects
    ]
  
  def process_joined_geometries(self):
    self.joined_geometries = Rhino.Geometry.Brep.JoinBreps(
      self.boundary_geometries, Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance
    )
  
  def calculate_boundary_mathemathics(self):
    boundary_bounding_box = Rhino.Geometry.BoundingBox.Empty

    for boundary_object in self.boundary_objects:
      object_bounding_box = boundary_object.Geometry.GetBoundingBox(True)
      boundary_bounding_box.Union(object_bounding_box)
    
    self.boundary_diagonal_length = boundary_bounding_box.Diagonal.Length
    self.boundary_global_center = boundary_bounding_box.Center
  
  def initialize_correlated_parameters(self):
    for boundary_object in self.boundary_objects:
      object_meshes = Rhino.Geometry.Mesh.CreateFromBrep(boundary_object.BrepGeometry, Winder.MINIMAL_MESHING_PARAMETERS)

      for object_mesh in object_meshes:
        for face_index in range(object_mesh.Faces.Count):
          center_point = object_mesh.Faces.GetFaceCenter(face_index)

          normal_vector = object_mesh.NormalAt(face_index, center_point.X, center_point.Y, center_point.Z, 0.0)

          positive_segment = Rhino.Geometry.Line(center_point, normal_vector, +self.boundary_diagonal_length)
          negative_segment = Rhino.Geometry.Line(center_point, normal_vector, -self.boundary_diagonal_length)

          line_max = Rhino.Geometry.Point3d(positive_segment.ToX, positive_segment.ToY, positive_segment.ToZ)
          line_min = Rhino.Geometry.Point3d(negative_segment.ToX, negative_segment.ToY, negative_segment.ToZ)
          
          vector_line = Rhino.Geometry.Line(line_min, line_max)
          vector_curve = vector_line.ToNurbsCurve()

          self.correlated_normal_vectors.append(normal_vector)
          self.correlated_center_points.append(center_point)

          self.correlated_vector_curves.append(vector_curve)
          self.correlated_curve_starts.append(line_min)
          self.correlated_curve_ends.append(line_max)

          self.correlated_object_ids.append(boundary_object.Id)

          self.correlated_count += 1
    
    for correlated_index in range(self.correlated_count):
      object_id = self.correlated_object_ids[correlated_index]
      self.objects_flip_puntuaction[object_id] = 0
  
  def calculate_flip_punctuations(self):
    for correlated_index in range(self.correlated_count):
      normal_vector = self.correlated_normal_vectors[correlated_index]
      center_point = self.correlated_center_points[correlated_index]

      vector_curve = self.correlated_vector_curves[correlated_index]
      curve_start = self.correlated_curve_starts[correlated_index]
      curve_end = self.correlated_curve_ends[correlated_index]

      object_id = self.correlated_object_ids[correlated_index]

      important_points = [curve_start, curve_end]

      for joined_geometry in self.joined_geometries:
        intersection_result = Rhino.Geometry.Intersect.Intersection.CurveBrep(
          vector_curve,
          joined_geometry,
          Rhino.RhinoMath.ZeroTolerance
        )
        
        intersection_points = intersection_result[2]

        for partial_intersection_point in intersection_points:
          important_points.append(partial_intersection_point)
      
      if len(important_points) % 2 == 0:
        aligned_points = sorted(important_points, key=lambda point: point.DistanceTo(curve_start))

        is_next_segment_inside = False
        
        for aligned_point_index in range(len(aligned_points) - 1):
          analyzing_point = aligned_points[aligned_point_index]

          if (analyzing_point.DistanceTo(center_point) < 0.001):
            dot_product = 0

            if is_next_segment_inside:
              posterior_point = aligned_points[aligned_point_index + 1]

              desired_vector = Rhino.Geometry.Vector3d(
                posterior_point.X - analyzing_point.X,
                posterior_point.Y - analyzing_point.Y,
                posterior_point.Z - analyzing_point.Z
              )

              dot_product = normal_vector * desired_vector

            else:
              anterior_point = aligned_points[aligned_point_index - 1]

              desired_vector = Rhino.Geometry.Vector3d(
                anterior_point.X - analyzing_point.X,
                anterior_point.Y - analyzing_point.Y,
                anterior_point.Z - analyzing_point.Z
              )
              
              dot_product = normal_vector * desired_vector

            if dot_product > 0:
              self.objects_flip_puntuaction[object_id] += 1
            
            if dot_product < 0:
              self.objects_flip_puntuaction[object_id] -= 1

          is_next_segment_inside = not is_next_segment_inside

      else:
        center_vector = Rhino.Geometry.Vector3d(
          center_point.X - self.boundary_global_center.X,
          center_point.Y - self.boundary_global_center.Y,
          center_point.Z - self.boundary_global_center.Z
        )
        
        normal_vector.Unitize()
        
        summation_vector = center_vector + normal_vector
        subtraction_vector = center_vector - normal_vector

        if summation_vector.Length > subtraction_vector.Length:
          self.objects_flip_puntuaction[object_id] -= 1
        
        if summation_vector.Length < subtraction_vector.Length:
          self.objects_flip_puntuaction[object_id] += 1

  def harmonize_boundary_normals(self):
    for object_id in self.objects_flip_puntuaction:
      flip_punctuation = self.objects_flip_puntuaction[object_id]

      analyzing_object = Rhino.RhinoDoc.ActiveDoc.Objects.FindId(object_id)

      new_attribures = analyzing_object.Attributes
      new_geometry = analyzing_object.BrepGeometry
      
      if flip_punctuation > 0:
        new_geometry.Flip()
      
      if self.layered_mode == True:
        if flip_punctuation > 0: new_attribures.LayerIndex = self.modifyed_layer_index
        if flip_punctuation < 0: new_attribures.LayerIndex = self.correct_layer_index
        if flip_punctuation == 0: new_attribures.LayerIndex = self.wrong_layer_index

      Rhino.RhinoDoc.ActiveDoc.Objects.Remove(analyzing_object)
      Rhino.RhinoDoc.ActiveDoc.Objects.Add(new_geometry, new_attribures)

  @staticmethod
  def create_layer_index(name, red, green, blue):
    layer = Rhino.DocObjects.Layer()
    
    layer.Name = name
    layer.Color = System.Drawing.Color.FromArgb(red, green, blue)
    layer.IsVisible = True
    layer.IsLocked = False

    layer_index = Rhino.RhinoDoc.ActiveDoc.Layers.Add(layer)

    return layer_index

if __name__ == "__main__":
  winder = Winder(layaered_mode=True)

  winder.set_selected_objects()
  winder.filter_boundary_objects()
  winder.pick_boundary_geometries()
  winder.process_joined_geometries()
  winder.calculate_boundary_mathemathics()
  winder.initialize_correlated_parameters()
  winder.calculate_flip_punctuations()
  winder.harmonize_boundary_normals()
  
  Rhino.RhinoDoc.ActiveDoc.Views.Redraw()