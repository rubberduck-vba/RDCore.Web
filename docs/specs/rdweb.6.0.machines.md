# 6.0 Machines

A machine is one runtime session of the application's program, and it runs one procedure at a time. Each machine has a state of its own: module-level
variables, `Static` locals, objects, open files, error state and current directory. Each request runs on a machine it has alone until its answer, so two
requests never share that state, and a procedure needs no locks. Requests run at the same time on several machines.

The endpoints of a controller with `'@KeptMachine` run on kept machines instead ([§6.2](#62-kept-machines)): each key has a machine of its own, and its
requests share that machine's state, one request at a time.

> [!NOTE]
> **Not implemented.** The RDCore.Web host composes no program ([§7.2](rdweb.7.0.hosting.md#72-the-rdcoreweb-host)), so no machine runs an RD-VBA
> procedure. The pool runs the machines of the program an application gives it.

---
## 6.1 The Machine Pool

### Static Semantics

The pool's size and limits are its options. A pool whose options are out of range is not started: `StartAsync` throws an `ArgumentOutOfRangeException`.

|Option|Default|Valid values|Meaning|
|---|---|---|---|
|`MinMachines`|`1`|From `0` to `MaxMachines`.|The machines the pool makes when it starts, and keeps once it has made them.|
|`MaxMachines`|The processor count.|`1` or more.|The machines the pool holds at most.|
|`RecycleAfter`|`1000`|`1` or more.|The requests a machine serves before the pool disposes it and makes another.|
|`RequestTimeout`|90 seconds|Positive.|How long a request may take, from its arrival to its answer, its wait for a machine included.|
|`StopTimeout`|5 seconds|Positive.|How long a machine has to stop, past the deadline of its request or of its reset, before the pool abandons it.|
|`ResetTimeout`|5 seconds|Positive.|How long a machine's reset may take.|

### Runtime Semantics

|When|The pool|
|---|---|
|It starts|Makes `MinMachines` machines, all idle.|
|A request arrives and a machine is idle|Runs the procedure on that machine.|
|No machine is idle, and the pool holds fewer than `MaxMachines`|Makes a machine for the request.|
|No machine is idle, and the pool holds `MaxMachines`|The request waits for a machine, without holding a thread. A pool of one machine serves its requests one at a time.|
|The client aborts a request while it waits|The request does not run, and the machine serves the next one.|
|The procedure completed, raised a run-time error, ran an `End` statement, or was not found|Resets the machine, then makes it idle.|
|The procedure ended any other way, or the machine failed|Recycles the machine: disposes it, and makes another.|
|The machine has served `RecycleAfter` requests|Recycles the machine.|
|A reset fails, or does not complete within `ResetTimeout`|Recycles the machine.|
|It is disposed|Takes no more requests. It disposes each idle machine at once, and each other machine when its request ends.|

A reset returns the machine to the state it was made in. It releases the module-level variables and `Static` locals, running the `Class_Terminate` of each
object it releases, closes the open files, clears the error state and restores the current directory. Nothing of one request reaches the next.

### Deadlines

Each deadline runs from the request's arrival, or from the start of a reset.

|Deadline|Passed while|Then|
|---|---|---|
|`RequestTimeout`|The request waits for a machine|The request is refused and never runs: `503 Service Unavailable` ([§5.3](rdweb.5.0.responses.md#53-other-outcomes)).|
|`RequestTimeout`|The procedure runs|The procedure is interrupted between two instructions, and the machine is recycled.|
|`RequestTimeout` and `StopTimeout`|The machine has not stopped|The pool abandons the machine and makes another. The request answers `500` as interrupted ([§5.3](rdweb.5.0.responses.md#53-other-outcomes)). The pool disposes the abandoned machine if it ever ends.|
|`ResetTimeout`|The reset runs|The reset is cut short, and the machine is recycled. A machine that has not stopped `StopTimeout` later is abandoned.|

A machine sees an interruption only once a call outside the program, to a `Declare` procedure or a COM object, returns. A machine that waits on one past its
deadlines is abandoned.

---
## 6.2 Kept Machines

A controller with `'@KeptMachine` ([§2.5](rdweb.2.0.annotations.md#25-keptmachine)) keeps a machine for each value of its key's route parameter. A request
runs on its key's machine, which is never reset: its module-level variables, its objects and the rest of its state are what the key's earlier requests left.
A game, a wizard or any other conversation of several requests keeps its state in a standard module.

### Static Semantics

The registry's limits are its options. A registry whose options are out of range is not made: its constructor throws an `ArgumentOutOfRangeException`.

|Option|Default|Valid values|Meaning|
|---|---|---|---|
|`MaxMachines`|`100`|`1` or more.|The machines kept at most.|
|`IdleTimeout`|20 minutes|Positive.|How long a machine is kept without a request.|
|`RequestTimeout`|90 seconds|Positive.|How long a request may take, from its arrival to its answer, its wait for its machine included.|
|`StopTimeout`|5 seconds|Positive.|How long a machine has to stop, past the deadline of its request, before it is abandoned.|

An application that maps the endpoints of such a controller gives `MapRDWebEndpoints` a registry. Mapping them without one throws an `ArgumentException`.

### Runtime Semantics

|When|The registry|Response|
|---|---|---|
|A request to a `'@NewMachine` endpoint, and its key has no machine|Makes the machine, then runs the procedure on it.|As [§5.0](rdweb.5.0.responses.md) describes.|
|The same, and its key has a machine|Runs nothing.|`409 Conflict`, with problem details.|
|The same, and `MaxMachines` machines are kept|Runs nothing, and keeps every machine.|`503 Service Unavailable`, with problem details and a `Retry-After` of one second.|
|A request to another endpoint, and its key has no machine|Runs nothing.|`404 Not Found`, with problem details.|
|A request to another endpoint, and its key has a machine|Runs the procedure on it once it is free: one request at a time, in the order they arrive. A request waits without holding a thread.|As [§5.0](rdweb.5.0.responses.md) describes.|
|The procedure completed, raised a run-time error, or was not found|Keeps the machine.|As [§5.0](rdweb.5.0.responses.md) describes.|
|The procedure ran an `End` statement|Disposes the machine: its key has none afterwards.|`204 No Content`.|
|The procedure ended any other way, or the machine failed|Disposes the machine.|As [§5.3](rdweb.5.0.responses.md#53-other-outcomes) describes.|
|A request waits for a machine that is then disposed|Runs nothing.|`404 Not Found`.|
|A machine has had no request for `IdleTimeout`|Disposes it. A request that holds its machine stops its idle time, and its answer starts it again.|None.|
|It is disposed|Takes no more requests. It disposes each free machine at once, and each other machine when its request ends.|None.|

The deadlines are those of the pool ([§6.1](#deadlines)), without a reset: a request that waits for its machine past `RequestTimeout` is refused and never
runs, one that runs past it is interrupted and its machine disposed, and a machine that has not stopped `StopTimeout` later is abandoned. A client that
aborts a request while it waits leaves the machine to the next one.

---
## Implementation

|Type or member|Role|
|---|---|
|[`RDWebMachinePool`](xref:RDCore.Web.Machines.RDWebMachinePool)|The pool: borrows a machine for each request, returns, resets, recycles and abandons them.|
|[`RDWebMachinePoolOptions`](xref:RDCore.Web.Machines.RDWebMachinePoolOptions)|The pool's size and limits.|
|[`RDWebKeptMachines`](xref:RDCore.Web.Machines.RDWebKeptMachines)|The kept machines: makes a machine for a key, runs each request of the key on it, and disposes it.|
|[`RDWebKeptMachinesOptions`](xref:RDCore.Web.Machines.RDWebKeptMachinesOptions)|The registry's limits.|
|[`RDWebKeptMachineAnswer`](xref:RDCore.Web.Machines.RDWebKeptMachineAnswer)|What a request to a kept machine reached: what the machine answered, or why it reached none.|
|[`IComposedProgram`](xref:RDCore.SDK.Runtime.Hosting.IComposedProgram)|The program the pool makes its machines from.|
|[`IProgramMachine`](xref:RDCore.SDK.Runtime.Hosting.IProgramMachine)|A machine: it runs a procedure, and resets.|

---
## Tests

|Rule|Test|
|---|---|
|A pool of one machine runs a second request once the first has returned|`RDWebMachinePoolTests.APoolOfOneMachine_RunsASecondRequestOnlyOnceTheFirstHasReturned`|
|Two machines run two requests at once|`RDWebMachinePoolTests.APoolOfTwoMachines_RunsTwoRequestsAtOnce_EachOnAMachineOfItsOwn`|
|The pool makes a machine for a request that finds none idle, up to its maximum|`RDWebMachinePoolTests.ThePool_MakesAMachineForARequestThatFindsNoneIdle_UpToItsMaximum`|
|An aborted request that waits does not run|`RDWebMachinePoolTests.ARequestCancelledWhileItWaits_DoesNotRun_AndTheMachineServesTheNextOne`|
|A machine is reset after each request|`RDWebMachinePoolTests.AfterEachRequest_TheMachineIsReset_BeforeItServesTheNextOne`|
|Completed, a run-time error, `End` and not found keep the machine|`RDWebMachinePoolTests.AMachineWhoseRequestEndedNormally_IsResetAndServesTheNextOne`|
|Any other outcome recycles the machine|`RDWebMachinePoolTests.AMachineWhoseRequestDidNotEnd_IsRecycled_AndAnotherServesTheNextOne`|
|A machine that fails is recycled|`RDWebMachinePoolTests.AMachineWhoseInvocationThrows_IsRecycled_AndAnotherServesTheNextOne`|
|A machine is recycled after `RecycleAfter` requests|`RDWebMachinePoolTests.AMachine_IsRecycledOnceItHasServedRecycleAfterRequests`|
|A reset that fails or does not complete recycles the machine|`RDWebMachinePoolTests.AMachineWhoseResetDoesNotComplete_IsRecycled`|
|A reset past its deadline is cut short, and a machine that does not stop is abandoned|`RDWebMachinePoolTests.AResetPastItsDeadline_IsCancelled_AndAMachineThatThenDoesNotStop_IsAbandonedAndReplaced`|
|A request past its deadline is interrupted, and its machine recycled|`RDWebMachinePoolTests.ARequestPastItsDeadline_IsCancelled_AndItsMachineRecycled`|
|A machine that does not stop is abandoned, and the request answers interrupted|`RDWebMachinePoolTests.ARequestWhoseMachineDoesNotStopAfterItsDeadline_AnswersInterrupted_AndItsMachineIsAbandonedAndReplaced`|
|A request that waits past its deadline is refused|`RDWebMachinePoolTests.ARequestThatWaitsForAMachinePastItsDeadline_IsRefused_AndNeverRuns`|
|Disposing the pool disposes its machines|`RDWebMachinePoolTests.DisposingThePool_DisposesItsIdleMachines_AndOneServingARequestOnceItEnds`|
|A pool of an invalid size is not started|`RDWebMachinePoolTests.APoolOfAnInvalidSize_IsRefused`|
|A pool with a limit that is not positive is not started|`RDWebMachinePoolTests.APoolWithALimitThatIsNotPositive_IsRefused`|
|A key's requests run on the machine its first made, never reset; two keys have two machines|`RDWebKeptMachinesTests.ARequestThatMakesAMachine_RunsOnIt_AndEveryLaterRequestOfItsKeyRunsOnTheSameMachine_NeverReset`, `RDWebKeptMachinesTests.TwoKeys_EachHaveAMachineOfTheirOwn`, `RDWebKeptMachinesHostingTests.ARequestThatMakesItsKeysMachine_RunsOnIt_AndTheKeysLaterRequestsRunOnTheSameMachine`|
|A key with no machine, or one that has one already, runs nothing|`RDWebKeptMachinesTests.ARequestForAKeyWithNoMachine_ReachesNone`, `RDWebKeptMachinesTests.ARequestThatMakesAMachineForAKeyThatHasOne_IsNotRun`, `RDWebKeptMachinesHostingTests.ARequestOfAKeyWithNoMachine_AnswersNotFound_AndMakesNone`, `RDWebKeptMachinesHostingTests.ARequestThatMakesAMachineForAKeyThatHasOne_AnswersConflict_AndRunsNothing`|
|A machine more than `MaxMachines` is refused, and no machine is evicted|`RDWebKeptMachinesTests.ARequestThatWouldMakeOneMachineMoreThanTheMaximum_IsRefused_AndTheKeptMachinesStay`, `RDWebKeptMachinesHostingTests.ARequestThatWouldMakeOneMachineMoreThanTheMaximum_AnswersServiceUnavailable_WithWhenToRetry`|
|A key's requests run one at a time, in the order they arrive|`RDWebKeptMachinesTests.TwoRequestsOfOneKey_RunOneAtATime_InTheOrderTheyArrive`, `RDWebKeptMachinesTests.ARequestOfAKeyWhoseMachineIsBeingMade_WaitsForIt_AndRunsOnIt`|
|Completed, a run-time error and not found keep the machine; anything else disposes it|`RDWebKeptMachinesTests.AMachineWhoseRequestEndedAsARunEnds_IsKept`, `RDWebKeptMachinesTests.AMachineWhoseRequestEndedAnyOtherWay_IsDisposed_AndItsKeyHasNoMachine`, `RDWebKeptMachinesTests.AMachineWhoseInvocationThrows_IsDisposed_AndItsKeyHasNoMachine`, `RDWebKeptMachinesTests.AMachineThatCannotBeMade_LeavesItsKeyFree`|
|`End` disposes the machine and answers `204`|`RDWebKeptMachinesHostingTests.AProcedureThatEndsItsMachine_AnswersNoContent_AndItsKeyHasNoMachineAfterwards`|
|A request that waits for a machine then disposed reaches none|`RDWebKeptMachinesTests.ARequestThatWaitsForAMachineThatThenEnds_ReachesNone`|
|A machine idle for `IdleTimeout` is disposed; a busy one is not idle|`RDWebKeptMachinesTests.AMachineWithNoRequestForItsIdleTimeout_IsDisposed_AndEachRequestRestartsTheTimeout`, `RDWebKeptMachinesTests.AMachineServingARequest_IsNotIdle`|
|The deadlines and an aborted wait|`RDWebKeptMachinesTests.ARequestThatWaitsForItsMachinePastItsDeadline_IsRefused_AndNeverRuns`, `RDWebKeptMachinesTests.ARequestCancelledWhileItWaits_DoesNotRun`, `RDWebKeptMachinesTests.ARequestWhoseMachineDoesNotStopAfterItsDeadline_AnswersInterrupted_AndItsMachineIsDisposedOnceItEnds`|
|Disposing the registry disposes its machines|`RDWebKeptMachinesTests.DisposingTheRegistry_DisposesAFreeMachineNow_AndABusyOneOnceItsRequestEnds`|
|A registry with a limit that is not positive is not made|`RDWebKeptMachinesTests.ARegistryWithALimitThatIsNotPositive_IsRefused`|
|Endpoints that keep machines need a registry|`RDWebKeptMachinesHostingTests.EndpointsThatKeepMachines_MappedWithNoKeptMachines_AreAnArgumentException`|
