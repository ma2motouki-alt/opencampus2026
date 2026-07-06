# Experience Specification

## Goal

Create an interactive exhibit for a horizontal rectangular display. The screen shows a small world inhabited by autonomous little people. Visitors do not directly control the characters. Hands and props become terrain, obstacles, rideable edges, shadows, or triggers inside the world.

## Target Feeling

- The world should feel alive even when nobody touches it.
- Little people normally live on the inset screen edge path.
- When a prop is placed nearby, little people should appear to notice it and move onto the prop edge when it is walkable.
- Menus, scores, missions, and explanatory UI are avoided. The reactions themselves are the interface.
- Real props may hide the display underneath them, so reactions should happen around object edges, nearby shadows, and the surrounding space.

## MVP Scope

The MVP uses mouse-created virtual objects instead of RealSense.

- Hand object: edge walkers are startled and reverse direction.
- Round prop: nearby little people become curious.
- Bar prop: the bar creates `WalkableSurface` rules on the real long edges of the visible rectangle. Little people transfer from the screen edge to an edge-side attach point, walk along the actual rectangle edge toward the screen-center-side corner, ride the surface during slow dragging, and either move directly onto a nearby bar surface or fall back to the display edge after reaching that corner.
- If another bar surface attach point is close to the walked surface tip, the little person should transfer to that prop instead of dropping to the ground. If no nearby surface is available, the fall should start from the center-side corner of the walked real rectangle edge.
- Little people should not appear to walk on the bar center line or on a separate light-blue debug path outside the bar.
- Bar prop tilt controls which sides can be boarded from. Near-vertical bars can be boarded from both sides; tilted bars expose only the screen-up side as walkable.
- Little people must approach from the walkable side of the bar. A little person on the opposite side should not appear to pass through the prop to board the surface.
- If an edge-walking little person hits a non-walkable side of the bar, it should turn around and continue along the display edge in the opposite direction instead of walking through the prop.
- The debug surface line, when shown, should overlap the actual bar lane and must not look like a separate platform.
- Cloud ambient object: when touched by a little person, rain falls below the cloud.
- Star ambient object: when touched by a little person, light bursts from the star.

Clouds and stars are ambient world objects, not input objects.
