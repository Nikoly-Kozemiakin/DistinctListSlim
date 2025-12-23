# DistinctListSlim<T>

A minimalist collection for **ValueTypes** with no allocations for small sets (<50 elements), using `stackalloc` and `ArrayPool`.

## Features
- ✅ Zero allocations for sets smaller than 50 elements  
- ✅ Supports uniqueness (`Distinct`)  
- ✅ Fast `Contains`, `Add`, `Remove` operations  
- ✅ Manual resource lifecycle control via `Release()` (required because it is a `ref struct` and cannot rely on GC finalization)  
- ✅ Switches seamlessly from stack to pooled array when capacity grows  

## Example Usage

```csharp
// Allocate buffer on the stack for 20 elements
Span<int> buffer = stackalloc int[20];

// Create DistinctListSlim over this buffer
var slim = new DistinctListSlim<int>(buffer, distinct: true);

// Add elements
slim.AddRange(new int[] { 1, 2, 3, 4 });

// Check existence
Console.WriteLine(slim.Contains(3)); // True
Console.WriteLine(slim.Contains(99)); // False

// Remove element
slim.Remove(3);
Console.WriteLine(slim.Contains(3)); // False

// Iterate over elements
foreach (var x in slim.AsSpan())
    Console.Write($"{x} ");

// IMPORTANT: release rented array if growth occurred
slim.Release();
```
## Resource Lifecycle
Because DistinctListSlim<T> is implemented as a ref struct, it cannot rely on GC finalization.
When the internal buffer grows beyond the stack allocation, it rents an array from ArrayPool<T>.
To avoid memory leaks, you must call Release() manually when the container is no longer needed.

```csharp
var buffer = stackalloc int[20]; 
var slim = new DistinctListSlim<int>(buffer, distinct: true); 
// use slim... 
slim.Release(); 
// return rented array to ArrayPool if allocated
```
