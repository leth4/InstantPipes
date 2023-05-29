# Instant Pipes

An editor tool for procedurally generating pipes by just dragging the cursor from start to end — the pipe will find the path in a customizable way.

![Unity_3wtlMTU9I1](https://github.com/letharqic/InstantPipes/assets/44412176/912f3879-1d82-4408-8cef-2698b82608a0)

## Compatibility

Unity 2021.3 or higher.

## Installation

Add the package to your project via the [Package Manager](https://docs.unity3d.com/Manual/upm-ui.html) using the Git URL
`https://github.com/letharqic/InstantPipes.git`. You can also clone the repository and point the Package Manager to your local copy.

## Usage

### Starting out

1. Create an empty GameObject and set its world position to zero.
2. Add a `Pipe Generator` component.
3. Select a material for the `Material` property.

**! If your pipes appear squashed, toggle and re-toggle rings, that will fix it. Looking into it!**

Ctrl+Z works with all actions. When you're commited to the pipes, you can just remove the component, the mesh will stay.

### Pipes Settings

- `Curvature` changes the length of the curved parts, making pipes appear more or less curvy. Note that it applies after pathfinding, so in some cases high curvature value can make pipes intersect.
- `Edges` property selects how many edges the pipes will have, and `Segments` is the amount of subdivisions in curved parts. 
- You can toggle `Rings` and `End Caps` and separately set up their radius and thickness.

### Using pathfinding

In the component inspector, select the `Create` tab. Now in the scene view start dragging your cursor where you want the pipe to start, end let go where you want it to end; a pipe will appear.

The tool uses A* pathfinding without a predefined grid — by raycasting from a point to the next points. Pipes can only detect colliders as obstacles.

Property | Explanation
:- | :-
Amount | How many pipes will be created at once; each one will have an individual path.
Max Iterations | How many points will the algorithm check before giving up.
Grid Size | The distance between searched points; making it too small can produce bad results.
Height | How high the first and the last segment of a pipe will be. This value can't be smaller than grid size.
Chaos | Adds randomness to the pathfinding, making paths twisted and chaotic.
Straight Priority | Makes the algorithm prefer straight paths over turns.
Near Obstacle Priority | Makes the pipes stay close to obstacles.

![image](https://github.com/letharqic/InstantPipes/assets/44412176/a076dcf6-21d2-46b1-80c9-70cdbd59b00e)

### Manual Editing

In the component inspector, select the `Edit` tab. Now you can select any point of any pipe by clicking on it, and then either move it in the scene view or delete the point, delete the entire pipe or insert a new point via buttons in the inspector. Hold shift to select multiple points.

Every pipe is a separate submesh, so you can assign separate materials by dragging them into the scene view.
