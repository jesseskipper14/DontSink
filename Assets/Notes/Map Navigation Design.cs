//using System.Buffers.Text;
//using Unity.VisualScripting;

//The cleanest summary is:

//Piloting is anticipation - based, not reaction-speed based.
//The helm uses a top-down virtual navigation view with sluggish boat-like movement.
//Throttle and rudder are physical persistent control states, not instantaneous movement commands.
//Players generate intent. The boat owns the actual persistent control/physics state.
//If a pilot dies/disconnects/leaves, throttle, rudder, heading, momentum, etc. do not reset.
//The piloting overlay is just a controller/view onto authoritative boat state.
//Propulsion requires a real available source. Right now that means an installed engine that is on and capable of producing thrust.
//Navigation gets its own state separate from side-view scene coordinates:
//along - track distance
//cross-track displacement
//heading/course
//eventually actual virtual/world navigation position
//Physical BoatScene X is not authoritative geography. It is the local physical representation of travel.
//Reaching nominal route distance does not automatically mean reaching the destination.
//Nodes/docks should become available when the boat actually enters their navigation/approach region.
//Getting lost is not necessarily a failure state. You can:
//burn extra fuel
//wander for a long time
//reach another node
//discover POIs
//deliberately explore
//Long-term, BoatScenes can become procedurally streamed/chunked so there effectively isn't an arbitrary end-of-ocean wall.
//Star charting becomes positional navigation. When lost, observations help determine where you actually are and how to recover your course.

//And the architectural separation underneath all of it is:

//PLAYER
//  ↓
//Control Intent
//  ↓
//Authoritative Boat Piloting State
//  ↓
//Navigation / Boat Simulation
//  ↓
//Physical BoatScene representation
//  ↓
//UI / Piloting overlay observes it

//That is the big-picture design I would preserve.