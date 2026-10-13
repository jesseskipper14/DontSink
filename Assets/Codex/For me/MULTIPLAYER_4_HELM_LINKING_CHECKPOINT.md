# MP.4 helm/station linking checkpoint

Implemented 2026-10-12. Manual acceptance pending; MP.4 remains in progress.

The ordinary L interaction now submits authenticated host link commands. Helm selections belong to individual actors. The host validates current interaction registry, hardpoint generation, authored access/range, standing state, and station occupancy before calling the existing HardpointInteractable/PilotChairInteractable link behavior. Selecting the same helm cancels selection. Linking/unlinking uses the normal prompts and targeted feedback. Completed chair links are replicated separately from installed-module state and applied after module manifests, including late join.

No production boat authoring or automatic station provisioning was added. Authored empty hardpoints can remain pre-wired. Persistent link save/reload, crew travel and reconnect remain MP.5 work. Full BoatScene/piloting acceptance is still paused at the user's request.

Implementation: NetworkSceneBoat.Linking.cs, normal interaction adapter and station cleanup. Protocol 14 requires matching builds.

Native validation evidence is recorded in MULTIPLAYER_4_SIX_PASS_ACCEPTANCE.md. Manual test: select an installed helm with L, then L on an empty chair; unlink with L on the linked chair without a pending selection. Try an occupied station, leave range, and join late. Another player's pending selection must not affect yours.
