DevkitServer plugin for creating zones for Uncreated Warfare.

https://github.com/UncreatedStaff/UncreatedWarfare

# Zone Editor
Create polygon, rectangular, cylindrical, and spherical zones.

* Move and resize zones in 3D space.
* Assign faction IDs to main bases and anti-maincamp zones.
* Separatly move a point used to spawn players teleporting to the zone.
* Set minimum and maximum heights (or infinite).

![image](https://github.com/user-attachments/assets/d8d29e29-7411-4a1c-a74b-c6327d94f1e1)

# Polygon Zone Editor
Edit polygon vertices from a top-down view.

* Move indivdual vertices with a high-resolution view of the ground below.
* See through roofs and obstructions by adjusting the clip plane.
* Snap to the grid or to preset angles based on the lines around the point.

## Controls
| Name                      | Key               |
| ------------------------- | ----------------- |
| Zoom in/out               | scroll wheel      |
| Pan                       | middle click      |
| Exit View                 | right click       |
| Reset camera              | F                 |
| Adjust clip plane*        | up/down arrow     |
| Snap clip plane to look   | ctrl + down arrow |
| Snap vertex to grid       | ctrl              |
| Drag point                | left click        |
| Delete dragging point     | delete            |
| Add new point             | E                 |

\* The clip plane can be used to see through buildings or other obstructions. The position of the plane is basically where the camera can start seeing objects. Ctrl + down arrow will snap the plane to the ground below the camera and it can be adjusted using the arrow keys from there.
![image](https://github.com/user-attachments/assets/44178f3a-25ab-4ede-81af-b17d978e7ab7)

# Zone Mapper
Connect zones and provide weights used for picking the zones for a game.

## Controls
| Name                      | Key               |
| ------------------------- | ----------------- |
| Zoom in/out               | scroll wheel      |
| Pan                       | middle click      |
| Exit View                 | right click       |
| Create Link               | left click        |
| Select Link               | left click        |
| Delete Link               | delete            |

* Define unidirectional links between zones to define a path used for automatic zone pathing.
* Add a weight to each path so some paths are chosen more than others.  
