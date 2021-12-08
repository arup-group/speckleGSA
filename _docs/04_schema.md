---
permalink: /docs/schema
title: "SpeckleStructural schema"
excerpt: "SpeckleStructural schema"
toc: false
---

This is still V1 documentation. We are going to update this page to V2.
{: .notice--danger}

If you are interested in exploring the SpeckleStructural schema in detail either [take a look at the C# code](https://github.com/arup-group/SpeckleStructural/tree/master/SpeckleStructuralClasses) or download the schema in [JsonSchema format]({{site.baseurl}}/assets/schema/SpeckleStructuralJsonSchema.zip).

The JsonSchema has been automatically created from the C# codebase. As such each object contains information related to the base Speckle objects that can likely be ignored.
{: .notice--primary}

## Schema comparison

| Category         | Speckle object name             | GSA                      | GSA 10 COM keyword |
|------------------|---------------------------------|--------------------------|--------------------|
| NodesAndElements | Structural0DSpring              | Ground spring            | EL                 |
| NodesAndElements | Structural1DElement             | 1d element / member      | EL / MEMB          |
| NodesAndElements | Structural1DElementPolyline     | 1d member                | MEMB               |
| NodesAndElements | Structural2DElement             | 2d element               | EL / MEMB          |
| NodesAndElements | Structural2DElementMesh         | 2d member                | MEMB               |
| NodesAndElements | Structural2DVoid                | 2d void cutter           | MEMB               |
| NodesAndElements | StructuralNode                  | Node                     | NODE               |
| Properties       | Structural1DProperty            | Section                  | PROP_SEC           |
| Properties       | Structural2DProperty            | 2d property              | PROP_2D            |
| Properties       | StructuralMaterialConcrete      | Concrete                 | MAT_CONCRETE       |
| Properties       | StructuralMaterialSteel         | Steel                    | MAT_STEEL          |
| Properties       | StructuralSpringProperty        | Springs                  | PROP_SPR           |
| Loads            | Structural0DLoad                | Node load                | LOAD_NODE          |
| Loads            | Structural0DLoadPoint           | Grid point load          | LOAD_GRID_POINT    |
| Loads            | Structural1DLoad                | Beam load                | LOAD_BEAM          |
| Loads            | Structural1DLoadLine            | Grid line load           | LOAD_GRID_LINE     |
| Loads            | Structural2DLoad                | 2d element face load     | LOAD_2D_FACE       |
| Loads            | Structural2DLoadPanel           | Grid area load           | LOAD_GRID_AREA     |
| Loads            | Structural2DThermalLoad         | 2d element thermal load  | LOAD_2D_THERMAL    |
| Loads            | StructuralGravityLoading        | Gravity load             | LOAD_GRAVITY       |
| Loads            | StructuralLoadCase              | Load case                | LOAD_TITLE         |
| Loads            | StructuralLoadCombo             | Load combination         | COMBINATION        |
| Loads            | StructuralLoadPlane             | Grid surface             | GRID_SURFACE       |
| Loads            | StructuralLoadTask              | Analysis task            | ANAL               |
| Loads            | StructuralLoadTaskBuckling      | Analysis task (buckling) | ANAL               |
| Results          | Structural1DElementResult       | (On a) 1d element        | (EL)               |
| Results          | Structural2DElementResult       | (On a) 2d element        | (EL)               |
| Results          | StructuralMiscResult            | (On a) assembly          | (ASSEMBLY)         |
| Results          | StructuralNodeResult            | (On a) node              | (NODE)             |
| Bridge           | Structural1DInfluenceEffect     | Beam influence effect    | INF_BEAM           |
| Bridge           | StructuralBridgeAlignment       | Alignment                | ALIGN              |
| Bridge           | StructuralBridgePath            | Path                     | PATH               |
| Bridge           | StructuralBridgeVehicle         | Vehicle                  | USER_VEHICLE       |
| Bridge           | StructuralNodalInfluenceEffect  | Nodal influence effect   | INF_NODE           |
| Miscellaneous    | StructuralAssembly              | Assembly                 | ASSEMBLY           |
| Miscellaneous    | StructuralConstructionStage     | Stage                    | ANAL_STAGE         |
| Miscellaneous    | StructuralReferenceLine         | Grid line                | GRID_LINE          |
| Miscellaneous    | StructuralRigidConstraints      | Rigid constraint         | RIGID              |
| Miscellaneous    | StructuralStagedNodalRestraints | Generalised restraints   | GEN_REST           |
| Miscellaneous    | StructuralStorey                | (Subset of) Grid planes  | GRID_PLANE         |