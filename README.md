# Pipe Tool

An editor tool for procedurally generating pipes by just dragging the cursor from start to end — the pipe will find the path in a customizable way.

## Compatibility

Unity 2021.3 or higher.

## Installation

Add the package to your project via the [Package Manager](https://docs.unity3d.com/Manual/upm-ui.html) using the Git URL
`https://github.com/letharqic/PipeTool.git`. You can also clone the repository and point the Package Manager to your local copy.

## Usage

### Starting out

1. Create an empty GameObject and set its world position to zero.
2. Add a `Pipe Generator` component.
3. Select a material for the `Material` property.

### Using pathfinding

In the component inspector, select the `Edit path` tab. Now in the scene view start dragging your cursor where you want the pipe to start, end let go where you want it to end; a pipe will appear.

The pathfinding can only detect objects with colliders on them. Pipes also detect each other.

Ctrl+Z works. Don't exceed the max vertex count.

### Manual Editing

Every pipe is its own submesh, so you can assign separate materials.

Select `Edit by hand` tab. You can select points and drag them.

### Properties

Property | Explanation
:- | :-
Radius | The radius of the pipes
Curvature | The length of the curved connections; the bigger the value, the more smooth the pipe will look.
Material | A default material that will be applied to all submeshes
Edges | How many edges will the pipe have
Segments | How many segments will a connector have


## TODO

### Pathfinding

- [ ] Handle edge cases, so there's always some path to be found.
- [ ] Iterations slider

### Bugs

- [ ] Fix Quaternion error

### Features

- [ ] Multiple pipes at once
- [ ] Selecting an area and filling it with pipes