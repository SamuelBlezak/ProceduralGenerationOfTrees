# Procedural Generation of Tree Models

This repository contains the source code for a bachelor thesis project focused on procedural generation of 3D tree models in Unity.

The system generates tree structures using stochastic L-systems, interprets them in 3D using turtle graphics, computes branch radii using a pipe model, creates a polygonal mesh, and supports procedural bark and leaf textures. It also includes basic interactive editing operations such as local growth, pruning, and adventitious bud creation.

## Features

- Procedural generation of tree skeletons using stochastic L-systems
- 3D turtle interpretation of generated strings
- Skeletal graph representation of the tree structure
- Branch radius computation using a pipe model
- Radius smoothing for more natural transitions between branches
- Mesh generation from cylindrical branch segments
- Adaptive radial segment count based on branch thickness
- Procedural bark and leaf texture generation
- Leaf generation at terminal nodes
- Interactive editing:
  - local growth using vigor
  - pruning of selected subtrees
  - creation of adventitious buds
- Export of generated trees to the `.OBJ` format
- Custom Unity inspector for easier parameter editing

## Repository contents

This repository mainly contains the implementation source files. Large Unity project files and generated data are not included in the repository in order to keep its size reasonable.

The complete Unity project, including all files required to run the scene, is included in the electronic attachment submitted together with the thesis.

## Requirements

The project was implemented in Unity using C#.

To run the complete project, use the Unity project included in the electronic attachment of the thesis. The GitHub repository is intended mainly for reviewing the implementation source code.

## Basic usage

1. Open the complete Unity project from the electronic attachment.
2. Open the provided sample scene.
3. Select the object containing the `TreeGenerator` component.
4. Choose a tree preset or manually adjust the generation parameters.
5. Generate or regenerate the tree using the controls in the custom inspector.
6. Optionally enter Play Mode to use interactive editing operations.
7. Export the generated model to `.OBJ` if needed.

## Main controls

The main parameters can be edited in the Unity inspector. They include, for example:

- number of L-system iterations
- branch angle
- segment length
- length decay
- trunk radius
- leaf density
- gravitropism
- phototropism
- random seed

Changing these parameters affects the generated tree shape, branch structure, leaf distribution, and overall visual appearance.

## Interactive editing

Interactive editing is performed in Play Mode. Depending on the selected mode, clicking on a branch can:

- add growth vigor to the selected part of the tree,
- remove the selected subtree,
- create adventitious buds near the selected branch.

After an edit, the skeletal graph is updated and the branch radii, mesh, leaves, collider data, and triangle-to-node mapping are recalculated.

## Export

Generated trees can be exported to the `.OBJ` format using the export option in the editor interface. This allows the resulting model to be used in other 3D tools or scenes.
