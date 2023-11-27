# [Winder](https://github.com/ruancarllo/winder) &middot; ![License](https://img.shields.io/badge/License-BSD--3--Clause_Clear-blue?style=flat-square) ![Framework](https://img.shields.io/badge/Framework-.NET_4.8-blueviolet?style=flat-square)

Winder is a plugin for the **harmonization of normal vectors** of solids manageable by [Rhinoceros](https://www.rhino3d.com), a powerful 3D modeling software. This process aims to orient all surfaces of a selected set of objects to their own exterior, preparing them for 3D printing.

<p align="center">
  <img src="./icons/winder-icon-1024px.png" width="250">
</p>

To achieve this, concepts and resources from the field of **Computational Geometry** are used, such as [boundary representation](https://en.wikipedia.org/wiki/Boundary_representation), [Riemann integral](https://en.wikipedia.org/wiki/Riemann_integral), and [dot product](https://en.wikipedia.org/wiki/Dot_product), along with many other programmatic techniques allowed by the languages [C#](https://dotnet.microsoft.com/en-us/languages/csharp), [Dart](https://dart.dev), and the [.NET Framework](https://dotnet.microsoft.com/en-us/learn/dotnet/what-is-dotnet-framework) — all in an optimized manner and within the processing realities of an industrial computer.

## Basic steps of the algorithm

The operation of the main command performed by Winder is based on the following steps, which occur interactively and visually due to the multiple layer colors created by the program during its execution. These steps are:

- Identification of selected objects and filtering those that can be represented as boundaries;
- Definition of generic parameters such as the central point and the diagonal size of the bounding box of these objects;
- Integration of boundary objects using the Riemann sum ray intersection method to find more reference points;
- Decision of the direction of each exploded surface based on vector operations such as dot product, addition, and subtraction.

## Tracing rays to find the inside

Without a doubt, the most important method for the operation of this algorithm is tracing **secant rays** to certain points of the boundary objects to find the internal regions of this figure. This can be done considering that if the number of points that the secant ray intersects with the joined objects is even, there is a pattern of alternation between their external and internal regions.

Note that the length of this ray is, by definition, twice the size of the **bounding box diagonal** representing the set of all analyzed objects, ensuring that its end points are indeed outside the analyzed solid.

<p align="center">
  <img src="./docs/ray-interception.jpg" width="350">
  <img src="./docs/integrated-polyhedron.jpg" width="350">
</p>

Thus, when a secant ray intersects perpendicularly with a point from which a normal vector of some surface emanates, upon identifying its nearest external region, it is possible to calculate the **dot product** ($d$) between the original normal vector $\vec{N} = [x, y, z]$ and the normal vector $\vec{N'} = [x', y', z']$ pointing to this external region.

$$
\begin{align*}
d &= \vec{N} \cdot \vec{N'} \\
d &= \sum_{i=1}^{3} n_i \times n'_i \\
d &= x \times x' + y \times y' + z \times z'
\end{align*}
$$

Consequently, the analysis of the sign of $d$ triggers three possible actions:

- $d > 0$: Vectors $\vec{N}$ and $\vec{N'}$ point in opposite directions → The surface should be flipped.
- $d < 0$: Vectors $\vec{N}$ and $\vec{N'}$ point in the same direction → The surface should remain as it is.
- $d = 0$: Vectors $\vec{N}$ and $\vec{N'}$ are perpendicular (forming 90°) → The surface yields an inconclusive result.

## Integrating to identify centroids

Certainly, the analysis of objects with small openings and overlapping surfaces is a very complex task, which can generate numerous geometric inconclusions — especially in cases where the secant ray intersects an odd number of objects, or the dot product is equal to zero.

In this context, the auxiliary idea of the **Riemann integral** is used to section the solid and define the **midpoints** of each of the rays of these partitions. This integral is calculated from the densities $d_x$ and $d_z$, which represent the number of **integration rays** that will cut through the figure, respectively, along the $x$ and $z$ axes.

Additionally, the minimum $B_0$ and maximum $B$ points of a **margin-bounded bounding box** of the set of analyzed objects are considered. This margin added to the original bounding box of the set ensures that the ends of the integration ray are indeed outside this solid.

Thus, the spacings $S_x$ and $S_z$ between the integration rays for the $x$ and $z$ axes are calculated by:

$$
S_x = \frac{B_x - B_0x}{d_x + 1} \\
$$

$$
S_z = \frac{B_z - B_0z}{d_z + 1}
$$

And the start $I_0$ and end $I$ points of each integration ray are defined by the following relationship:

$$
\sum I_0 = \sum_{i_x=1}^{d_x} \sum_{i_z=1}^{d_z} \begin{bmatrix} B_0x + S_x \times i_x \\ B_0y \\ B_0z + S_z \times i_z \end{bmatrix} \\
$$

$$
\sum I = \sum_{i_x=1}^{d_x} \sum_{i_z=1}^{d_z} \begin{bmatrix} B_0x + S_x \times i_x \\ B_y \\ B_0z + S_z \times i_z \end{bmatrix}
$$

Subsequently, these integration rays are subdivided into segments that will alternate between the external and internal parts of the figure if they intersect an even number of objects. From these segments, midpoints are defined that act as a possible reference to determine the direction of the normal vector of its nearest surface.

> Note that this process of finding a point belonging to the internal region of the solid is closely related to the idea of transferring the generic reference from the **center** of the bounding box of this solid to a reference closer to a **centroid** of a certain surface.

Thus, if the inconclusive surface analyzed is a boundary — meaning the point from which its normal vector emanates is the second or penultimate among the points its secant ray intersects — the reference for the direction of this normal vector remains the generic center of the bounding box. Otherwise, the centroid calculated by integration is used as a reference.

In both approaches, a vector $\vec{V}$ is considered that starts from this reference point and goes to the point from which the normal vector $\vec{N}$ of the surface emanates, modeling three situations:

- $\vert \vert \vec{V} + \vec{N} \vert \vert < \vert \vert \vec{V} - \vec{N} \vert \vert$: Vector sum is smaller than vector subtraction → The surface should be flipped.
- $\vert \vert \vec{V} + \vec{N} \vert \vert > \vert \vert \vec{V} - \vec{N} \vert \vert$: Vector sum is larger than vector subtraction → The surface should remain as it is.
- $\vert \vert \vec{V} + \vec{N} \vert \vert = \vert \vert \vec{V} - \vec{N} \vert \vert$: Vector sum is equal to vector subtraction → The surface yields an inconclusive result.

## Installing and running the software

Having explained all these logical-mathematical reasoning, it is time to execute this command on a real model or on a [example object](./mocks/example.3dm). To do this, ensure that your [Windows](https://en.wikipedia.org/wiki/Microsoft_Windows) or [MacOS](https://en.wikipedia.org/wiki/MacOS) computer has **Rhinoceros 7**, **Dart SDK 3**, and **.NET Framework 4.8** installed and properly configured in the `$PATH` environment variable of your shell.

To compile the source code into an assembly readable by Rhinoceros as a **.rhp** extension plugin available in the [bin](./bin) folder, open a terminal in the main folder of this project and run the following commands:

```shell
dotnet restore
dotnet publish
```

To compile the plugin, install it on your machine, and open an instance of Rhinoceros, open the terminal in the source folder of this project and run the debugging script with the following command:

```shell
dart scripts/debug.dart
```