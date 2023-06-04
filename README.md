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

If you're facing problems, visit the troubleshooting section.

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
Grid Y Angle | Rotates the Y axis of the pathfinding grid that every pipes have to follow.
Grid Size | The distance between searched points; making it too small can produce bad results.
Height | How high the first and the last segment of a pipe will be. This value can't be smaller than grid size.
Chaos | Adds randomness to the pathfinding, making paths twisted and chaotic.
Straight Priority | Makes the algorithm prefer straight paths over turns.
Near Obstacle Priority | Makes the pipes stay close to obstacles.

![image](https://github.com/letharqic/InstantPipes/assets/44412176/a076dcf6-21d2-46b1-80c9-70cdbd59b00e)

### Manual Editing

In the component inspector, select the `Edit` tab. Select one of the points in the scene view by clicking on it, and then you can:
- Move the selected points in the scene view
- Input the exact positions for the selected points in the inspector
- Delete the point or the entire pipe via a button in the inspector
- Insert a new point via a button in the inspector

Hold `shift` to select multiple points. Press `A` to select every point of the selected pipe.

Every pipe is a separate submesh, so you can assign separate materials by dragging them into the scene view.

## Troubleshooting

> Pipes appear squashed

- Toggle and re-toggle rings, that should fix it. Will hopefully find a proper fix soon.

> Dragging my cursor doesn't do anything

- Make sure you have the `Create` tab selected
- Make sure you're pointing at a surface with a collider
- Make sure Gizmos are enabled
- If nothing else helped, try resetting the editor layout to default

> Pipes can't find a way

- Set the `Iterations` higher, that will make the algorithm try for longer before giving up
- Set the `Grid Size` higher, this way the algorithm can find a way with less iterations
- Tone down the `Chaos`, `Straight Priority` and `Near Obstacle Priority` values, those make it harder to find a way
