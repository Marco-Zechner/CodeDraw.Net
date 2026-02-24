TODO: GPU SDF rendering architecture (single-source formulas + CSG)

Goal
- Keep SDF formulas in one place (C#), but render on GPU (GLSL).
- Support "#include"-style shader composition + hot reload.

- GPU uses a generic SDF fragment shader that:
    - evaluates a shape tree (or instruction list) per fragment
    - returns signed distance d
    - converts d to alpha (fill/outline/shadow/etc)

1) Represent GPU-evaluable SDFs
- Add to each primitive struct (Rect, Circle, Segment, RoundedRect, Polygon?):
    - a ShapeType enum id (int)
    - a method EmitGpuNode(builder) that:
        - appends a “node” (type + params) into a node buffer/list
    - optionally: a GLSL snippet string (const) that defines sd<Type>(p, params)

- For CSG structs (Union/Intersect/Subtract/SmoothUnion):
    - do NOT emit new geometry; emit an “op node” referencing child nodes
    - store op type + (optional) K for smooth union

2) GPU evaluation strategy for multiple SDFs
   “SDF program” = nodes + ops evaluated by a stack VM (single pass, no recursion)
- Compile the ISdf2 tree into a linear instruction list (postorder):
    - PUSH_SHAPE nodeIndex
    - OP_UNION
    - OP_INTERSECT
    - OP_SUBTRACT
    - OP_SMOOTH_UNION(k)
- In shader:
    - keep a small float stack (fixed max depth)
    - for PUSH_SHAPE: evaluate sdShape(p, nodeParams) and push distance
    - for OP_*: pop distances, combine (min/max/etc), push result
    - final stack top is the distance d

3) Shader composition: single-source formulas
   Approach 1: generated GLSL from C# const strings
- Each primitive provides:
    - const string GlslFunc = "float sdRect(vec2 p, vec4 params0, ...){...}"
- Shader build step:
    - gather all unique GlslFunc blocks from used primitive types
    - concatenate into shader header before main()
    - compile the final shader source

Approach 2: custom #include support
- Implement a preprocessor in your shader loader:
    - parse lines starting with #include "path"
    - replace with file contents (from disk in dev, embedded resources in release)
    - support nested includes + include guards (track included paths)
- Then keep sdf funcs in a shared file:
    - sdf_common.glsl
    - sdf_rect.glsl, sdf_circle.glsl, ...
- Shader uses #include "sdf_common.glsl"

4) Hot reload
- If using file-based includes:
    - watch shader files + included files → recompile on change
- If using C#-embedded GLSL strings:
    - simplest: rebuild app to reload (no true hot reload)
    - dev workaround: put GLSL snippets in external .glsl files, include them, avoid C# strings
    - or advanced: add a small “shader snippet registry” that can reload from disk while keeping C# mapping (still needs file watcher)

Pragmatic recommendation:
- Use #include + external .glsl files for real hot reload.
- Keep the “single-source” promise by making C# generate/validate params, not by storing shader code in C#.

5) Data layout sent to GPU
- Draw each SDF as a bounding quad (2 triangles) so we shade only needed pixels.
- Per-instance buffer (instancing) contains:
    - world->local transform (or local->world and invert in shader)
    - bounds (for quad) OR quad vertices already
    - style params (fill, outline thickness, softness, colors)
    - program offset + program length (where in the global instruction buffer this SDF’s program starts)

- Global buffers:
    - Node buffer: packed params for each primitive node (type + params)
    - Instruction buffer: uint/int opcodes + references + smoothK
    - (Optional) table mapping shapeType -> function index; or switch(shapeType)

6) GLSL evaluation functions (must match CPU)
- Primitive distance functions:
    - sdRect(p, rectParams)
    - sdRoundRect(p, rectParams, radius)
    - sdCircle(p, center, radius)
    - sdSegment(p, A, B, radius)
    - (avoid polygon/polyline initially; they require loops and can be expensive)

- CSG ops:
    - union: min(a,b)
    - intersect: max(a,b)
    - subtract: max(a, -b)
    - smooth union: smin(a,b,k)  (copy same formula as CPU)

7) Rendering (alpha from distance)
- In fragment shader:
    - d = EvalProgram(pLocal, programOffset, programLen)
    - aa = fwidth(d) (or derive from pixel size in local space)
    - fillAlpha = smoothstep(0, aa, -d)
    - outlineAlpha = smoothstep(0, aa, abs(d) - thickness)  (invert as needed)
    - combine with colors

8) CPU-side “compiler” from ISdf2 to GPU program
- Add interface like:
    - IGpuSdf2 { void Emit(GpuSdfBuilder b); Rect LocalBounds; }
- Builder produces:
    - list of nodes
    - list of instructions (stack VM)
    - returns (programOffset, programLen, bounds)

- Emission rules (postorder):
    - Primitive:
        - nodeIndex = AddNode(type, params)
        - Emit PUSH(nodeIndex)
    - Union(A,B):
        - A.Emit
        - B.Emit
        - Emit OP_UNION
    - Intersect/Subtract similarly
    - SmoothUnion:
        - A.Emit
        - B.Emit
        - Emit OP_SMOOTH_UNION(k)

9) Debug/validation
- Debug mode:
    - sample a few points p
    - compare CPU DistanceLocal(p) with GPU EvalProgram(p)
    - show max error / log mismatches (epsilon)

10) Roadmap / scope control
- Start GPU support with:
    - Rect, RoundedRect, Circle, Segment
    - Union/Intersect/Subtract/SmoothUnion
- Add polygons/polylines later (they can be heavy per fragment; may need distance-to-edge loop limits or texture SDF fallback)