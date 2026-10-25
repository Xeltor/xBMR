# Camera control handoff

Camera assistance temporarily borrows the game's legacy movement mode. Manual
movement, turning, the movement escape hatch, or loss of automation authority
restores the previous mode. Mouse, camera-key and right-stick input now restore
it in the camera input hook, before the native reader when raw input is available.
That raw check also includes movement/turning keys and both gamepad sticks, so
the first manual input does not depend on the previous movement observation.
Native camera deltas are checked after that reader; the previous automatic deltas
must not be mistaken for player input before it runs.

The camera and borrowed option share one three-second pause after the latest
manual input. Continued input extends that pause; idle framework updates do not.
This avoids both early re-borrowing and the former second pause caused by feeding
an existing camera pause back as fresh manual input. A player change to the
movement mode remains authoritative when the borrowed option is released.

`NativeMovementRegression/Presentation.cs` covers immediate option restoration,
continued input, the shared resume deadline, loss of authority and concurrent
player edits. These policy checks and a Release build do not establish live
keyboard, mouse or controller acceptance.
