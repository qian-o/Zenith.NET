# Queries

Queries report information produced while the GPU executes commands. Create a query heap from the same `GraphicsContext` as the command buffer, record query operations, and read the results after the submission has completed.

## Create a Query Heap

Choose a query heap description for the result you need:

| Description | Result | Recording commands |
|-------------|--------|--------------------|
| `QueryHeapDesc.Occlusion` | Number of samples that pass the visibility tests | `BeginQuery` and `EndQuery` |
| `QueryHeapDesc.BinaryOcclusion` | Whether any sample passes the visibility tests | `BeginQuery` and `EndQuery` |
| `QueryHeapDesc.Timestamp` | GPU timestamp values | `WriteTimestamp` |

The count in the description is the number of query slots. Use a separate index for each result that must remain available at the same time:

```csharp
QueryHeap queryHeap = context.CreateQueryHeap(QueryHeapDesc.Occlusion(1));
```

Keep the query heap alive until every submitted command that refers to it has completed.

## Record Occlusion Queries

While a render pass is active, surround the drawing commands whose visibility you want to measure with `BeginQuery` and `EndQuery`. Keep both query commands in the same render pass:

```csharp
commandBuffer.BeginRenderPass([ColorAttachment.Load(colorTarget)], null);

commandBuffer.BeginQuery(queryHeap, 0);
commandBuffer.SetPipeline(pipeline);
commandBuffer.Draw(vertexCount, 1, 0, 0);
commandBuffer.EndQuery(queryHeap, 0);

commandBuffer.EndRenderPass();
```

Use an occlusion heap for a sample count or a binary occlusion heap when only a visible-or-not result is needed. Submit the command buffer before reading the result.

## Record Timestamps

Write two timestamps around the work whose GPU duration you want to measure. Timestamp results are raw `ulong` values until the queue converts their difference:

```csharp
QueryHeap queryHeap = context.CreateQueryHeap(QueryHeapDesc.Timestamp(2));
CommandBuffer commandBuffer = context.ComputeQueue.CommandBuffer();

commandBuffer.WriteTimestamp(queryHeap, 0);
commandBuffer.SetPipeline(pipeline);
commandBuffer.Dispatch(groupCountX, groupCountY, groupCountZ);
commandBuffer.WriteTimestamp(queryHeap, 1);

TimelineValue completion = commandBuffer.Submit();
completion.Wait();
```

Read the two results and ask the same queue that recorded them for the elapsed time:

```csharp
Span<ulong> timestamps = stackalloc ulong[2];
queryHeap.GetResults(timestamps, 0);

double elapsedNanoseconds = context.ComputeQueue.GetElapsedNanoseconds(
    timestamps[0],
    timestamps[1]);
```

The two timestamp values must come from the same queue. `GetElapsedNanoseconds` returns the elapsed GPU time between the values; it does not represent a wall-clock time.

## Read Results

Wait for the submission that writes the queries before calling `GetResults`:

```csharp
Span<ulong> results = stackalloc ulong[1];
queryHeap.GetResults(results, 0);
```

The span length selects how many consecutive results to read, and `startIndex` selects the first query slot. Keep the span within the query heap's count. Reuse a slot only after the submission that last wrote it has completed.

## Manage Query Lifetime

Query heaps are application-owned resources. Dispose them after the command submissions that use them have completed. Command buffers remain queue-owned borrows; submit them before reading their query results and do not dispose them directly.

Queries use the same recording and completion model as other commands. Review [Commands](commands.md) for command-buffer lifetime and [Synchronization](synchronization.md) for submission dependencies.