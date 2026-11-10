# Proposed technical evaluation procedure

This document is a procedure for new tests. It contains no completed Unity trials.
Choose the repetition count, expected outcomes, observation window, and test
conditions before running. Ten runs per condition is a manageable initial choice,
not a statistical guarantee. Record the actual count and every failure.

## Preparation and records

Use one host and one client first. Run separate experiments with four total
participants if desired. Record editor version, resolved package lock, build
hash, operating systems, devices, LAN/Wi-Fi/Ethernet configuration, and whether
all processes run on one computer. Retain the same configuration across paired
conditions. The delivered project does not inject or measure network delay/loss.

Give every run a unique label (for example `late-corrected-001`) and set the
same label in both processes. Set scenario labels before recording. Keep a
separate trial sheet identifying independent trials; multiple clicks in one run
are not repetitions. Copy each process's CSV to the collected-results folder.

Check that there are no compile errors and both roles connect before conducting
the comparisons. A compiler error or unsuccessful connection is a failed setup,
not a successful experiment. Keep the failed setup log.

## 1 Late-join baseline and correction

1. Start a fresh host and wait for Scene A. Advance to stop 2.
2. Start the client with the initial-state omission switch enabled **before**
   clicking Start Client. Do not advance the host again after the client joins.
3. Wait up to the predeclared readiness window, for example 10 seconds. Record
   scene name, network stop, applied stop, and placement check on the client.
4. The intentionally faulty path is expected to receive stop 2 but fail to apply
   it. The scored placement check should fail. This is an expected baseline
   failure, which must remain in the raw logs; explain it as such in analysis.
5. End the session and repeat the same steps with omission disabled. The client
   is expected to apply stop 2 and pass the scene/stop/origin-placement check.
6. Repeat with fresh processes and new run labels for the chosen repetitions.
   Also test joining after a completed Scene B transition, as a separate condition.

Do not equate this reconstructed comparison with reproduction of the unavailable
historical incident or original Photon build.

## 2 Authority and shared interaction

1. Connect both processes and wait for readiness.
2. As guest, request next stop. Expect rejection and no changed stop.
3. Set `Expected latest server reply: accepted` to false and record the reply
   check after the reply is shown. Record the displayed stop in both processes.
4. As guide, request next stop. Expect acceptance and the same new stop on both.
5. Enable student permission as guide. As guest, request one shared action;
   expect acceptance and counter increment by exactly one on both processes.
6. Disable student permission as guide. Repeat the guest action; expect rejection
   and no counter increment. Record expected reply checks and displayed counters.
7. Assess requests one at a time. Simultaneous-request and malicious-client tests
   are separate experiments requiring their own acceptance criteria.

RPC receipt is not proof of every authorization invariant. Inspect raw accepted/
rejected events and `shared_count` on both devices, including unchanged state
after rejected actions. The client-provided operation never supplies identity;
the server uses RPC sender metadata.

## 3 Scene transitions and persistence

1. At stop 2, record a placement check in both processes.
2. As host request Scene B. Record the load-request status and local/group scene
   events. Wait for required readiness. Record placement on each process.
3. Compare destination, tour stop, counter, local rig count, and camera count.
   A completed local load alone does not establish all-client readiness.
4. Return to Scene A and repeat the predefined A/B sequence. Define a cycle as
   one A-to-B-to-A sequence before counting independent repetitions.
5. Record timed-out clients and failed load statuses. Inspect scene-group records
   on the server; capture client-specific completion events separately.
6. After reaching B at stop 2, join a new client and check reconstruction of the
   retained shared state. This tests the new persistent-session example only.

The primitive local rig is not a full XR rig. Confirm headset camera/controller
lifecycles separately if a headset integration is added.

## 4 Participant target association

1. Connect two processes; leave the wrong-remote-input switch disabled.
2. Participant A varies head yaw and one hand slider at a time while B leaves all
   input sliders fixed and observes A's remote targets.
3. B records the manual avatar PASS/FAIL judgment, with an explanation retained
   in the trial sheet. Exchange roles and repeat.
4. Enable wrong-remote-input rendering on the observer. Repeat the movement;
   verify that the deliberately faulty view fails participant association.
5. Disable the switch and repeat the corrected check. Keep recordings if useful,
   label them with build/run/role, and retain all failures.

This tests input association using primitive targets. It does not measure skeletal
accuracy, solve lower-body inference, evaluate interpolation, or reproduce the
historical ride project. Do not describe it as full-body IK validation.

## 5 Host loss

1. At a declared scene and stop, disconnect the guide host.
2. Wait for the declared disconnect observation window. On the former client,
   record whether the session ended and what remains displayed.
3. Use the session-ended check only after the transport has stopped. Preserve
   disconnect and shutdown events. This checks termination, not recovery.
4. No replacement-host/checkpoint recovery exists. A retained local scene is not
   evidence that a multiplayer lesson continued.

## 6 Offline execution

Start a fresh process in Offline mode; visit the three stops and record placement.
Record local scene transitions separately if added. Offline behavior is not a
networked tour and is not a performance comparison with the online mode.

## Reporting

Present attempted/completed/failed run counts by scenario and condition. Do not
silently exclude setup failures, timeouts, missing client logs, or repeated checks.
Distinguish expected injected-fault failures from unexpected corrected-path
failures. State that new reconstructed tests were conducted after the migration.
No human symptom or educational outcome is assessed by this protocol.
