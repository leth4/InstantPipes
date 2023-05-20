# Pipe Tool

An editor tool for procedurally generating pipes by just dragging the cursor from start to end — the pipe will find the path in a customizable way.

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

After you're done, you can just delete the component.

### Using pathfinding

In the component inspector, select the `Create` tab. Now in the scene view start dragging your cursor where you want the pipe to start, end let go where you want it to end; a pipe will appear.

The tool uses A* pathfinding without a predefined grid — by raycasting from a point to the next points. Pipes can only detect colliders as obstacles.

Property | Explanation
:- | :-
Grid Size | The distance between searched points; making it too small can produce bad results.
Height | How high the first and the last segment of a pipe will be. This value can't be smaller than grid size.
Chaos | Adds randomness to the pathfinding, making paths twisted and chaotic.
Straight Priority | Makes the algorithm prefer straight paths over turns.
Near Obstacle Priority | Makes the pipes stay close to obstacles.
Max Iterations | How many points will the algorithm check before giving up.
Auto Regenerate | If turned on, any changes to path properties will make it regenerate.

### Manual Editing

In the component inspector, select the `Edit` tab. Now you can select any point of any pipe by clicking on it, and then either move it in the scene view or delete the point, delete the entire pipe or insert a new point via buttons in the inspector.

You can select and edit multiple points of a pipe by holding `Shift`.

Every pipe is its own submesh, so you can assign separate materials by dragging them into the scene view.

### TODO

- [ ] Images for readme
- [ ] It doesn't show failed builds anymore