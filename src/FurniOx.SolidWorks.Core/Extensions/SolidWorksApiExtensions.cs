using System;

namespace FurniOx.SolidWorks.Core.Extensions;

/// <summary>
/// Extension methods for safely handling COM SafeArray returns in .NET 10+
///
/// BACKGROUND:
/// .NET 10 marshals COM SafeArrays as System.Object[*] (non-zero-based arrays)
/// instead of object[] (zero-based vectors). This causes InvalidCastException
/// when using direct casts like (object[]) or pattern matching like 'is object[]'.
///
/// SOLUTION (3 CRITICAL STEPS):
/// 1. Cast to Array base class (System.Object[*] IS an Array)
/// 2. Use Array.Copy() to perform type conversion during copy
/// 3. Try-catch for defensive error handling (resilience, not core fix)
///
/// IMPORTANT: Array.Copy() and CopyTo() are functionally identical.
/// CopyTo() internally calls Array.Copy(). The fix works due to:
/// - Casting to Array base class (avoids type mismatch)
/// - Type conversion during copy (handles variant arrays)
/// NOT because Array.Copy() is superior to CopyTo().
///
/// REFERENCES:
/// - .NET 10 Breaking Change: https://learn.microsoft.com/en-us/dotnet/core/compatibility/interop/8.0/cominterop-safearrays
/// - Research Findings: docs/NET10_SAFEARRAY_FINDINGS.md
/// - Array.CopyTo() Implementation: https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Array.cs
/// </summary>
public static class SolidWorksApiExtensions
{
    /// <summary>
    /// .NET 10 safe casting for COM SafeArray to object[]
    ///
    /// USAGE:
    ///   var segments = sketch.GetSketchSegments().ToObjectArraySafe();
    ///
    /// REPLACES:
    ///   var segments = (object[])sketch.GetSketchSegments(); // FAILS in .NET 10
    /// </summary>
    /// <param name="comArrayResult">COM SafeArray result from SolidWorks API</param>
    /// <returns>Properly typed object[] or null if input is null</returns>
    public static object[]? ToObjectArraySafe(this object? comArrayResult)
    {
        if (comArrayResult == null)
        {
            return null;
        }

        try
        {
            Array safeArray = (Array)comArrayResult;
            object[] result = new object[safeArray.Length];
            Array.Copy(safeArray, result, safeArray.Length);
            return result;
        }
        catch
        {
            // If casting or copying fails, return null
            return null;
        }
    }

    /// <summary>
    /// .NET 10 safe casting for COM SafeArray to double[]
    ///
    /// USAGE (for methods that return SafeArray of doubles):
    ///   var values = dimension.GetSystemValue3(...).ToDoubleArraySafe();
    ///
    /// REPLACES:
    ///   var values = (double[])dimension.GetSystemValue3(...); // FAILS in .NET 10
    ///
    /// NOTE: GetStartPoint2(), GetEndPoint2(), GetCenterPoint2() return ISketchPoint objects,
    ///       NOT SafeArrays! Use direct property access: point.X, point.Y, point.Z
    /// </summary>
    /// <param name="comArrayResult">COM SafeArray result from SolidWorks API</param>
    /// <returns>Properly typed double[] or null if input is null</returns>
    public static double[]? ToDoubleArraySafe(this object? comArrayResult)
    {
        if (comArrayResult == null)
        {
            return null;
        }

        try
        {
            Array safeArray = (Array)comArrayResult;
            double[] result = new double[safeArray.Length];
            Array.Copy(safeArray, result, safeArray.Length);
            return result;
        }
        catch
        {
            // If casting or copying fails, return null
            return null;
        }
    }

    /// <summary>
    /// .NET 10 safe casting for COM SafeArray to int[]
    ///
    /// NOTE: This is for COM SafeArrays ONLY. Regular .NET arrays like
    /// segment.GetID() which returns int[2] do NOT need this method.
    ///
    /// USAGE:
    ///   var indices = someComMethod().ToIntArraySafe();
    ///
    /// REPLACES:
    ///   var indices = (int[])someComMethod(); // FAILS in .NET 10 (if COM SafeArray)
    /// </summary>
    /// <param name="comArrayResult">COM SafeArray result from SolidWorks API</param>
    /// <returns>Properly typed int[] or null if input is null</returns>
    public static int[]? ToIntArraySafe(this object? comArrayResult)
    {
        if (comArrayResult == null)
        {
            return null;
        }

        try
        {
            Array safeArray = (Array)comArrayResult;
            int[] result = new int[safeArray.Length];
            Array.Copy(safeArray, result, safeArray.Length);
            return result;
        }
        catch
        {
            // If casting or copying fails, return null
            return null;
        }
    }

    /// <summary>
    /// .NET 10 safe check if COM result is a SafeArray
    ///
    /// USAGE:
    ///   if (pointsVal.IsSafeArray())
    ///   {
    ///       var points = pointsVal.ToObjectArraySafe();
    ///   }
    ///
    /// REPLACES:
    ///   if (pointsVal is object[] points) // FAILS in .NET 10 (pattern match returns false)
    /// </summary>
    /// <param name="comResult">Result from COM API call</param>
    /// <returns>True if result is a SafeArray, false otherwise</returns>
    public static bool IsSafeArray(this object? comResult)
    {
        return comResult != null && comResult is Array;
    }

    /// <summary>
    /// Get safe count of COM SafeArray elements
    ///
    /// USAGE:
    ///   var count = sketch.GetSketchSegments().SafeArrayCount();
    ///
    /// REPLACES:
    ///   var segmentsObj = sketch.GetSketchSegments();
    ///   var count = segmentsObj is object[] arr ? arr.Length : 0; // FAILS in .NET 10
    /// </summary>
    /// <param name="comArrayResult">COM SafeArray result</param>
    /// <returns>Number of elements or 0 if null</returns>
    public static int SafeArrayCount(this object? comArrayResult)
    {
        if (comArrayResult == null)
        {
            return 0;
        }

        if (comArrayResult is not Array safeArray)
        {
            return 0;
        }

        return safeArray.Length;
    }

    /// <summary>
    /// .NET 10 safe casting for COM SafeArray to string[]
    ///
    /// USAGE (for methods like CustomPropertyManager.GetNames()):
    ///   var names = propMgr.GetNames().ToStringArraySafe();
    ///
    /// REPLACES:
    ///   var names = (string[])propMgr.GetNames(); // FAILS in .NET 10
    /// </summary>
    /// <param name="comArrayResult">COM SafeArray result from SolidWorks API</param>
    /// <returns>Properly typed string[] or empty array if null/empty</returns>
    public static string[] ToStringArraySafe(this object? comArrayResult)
    {
        if (comArrayResult == null)
        {
            return Array.Empty<string>();
        }

        try
        {
            Array safeArray = (Array)comArrayResult;
            if (safeArray.Length == 0)
            {
                return Array.Empty<string>();
            }

            string[] result = new string[safeArray.Length];
            for (int i = 0; i < safeArray.Length; i++)
            {
                // GetValue handles non-zero-based arrays correctly
                var val = safeArray.GetValue(safeArray.GetLowerBound(0) + i);
                result[i] = val?.ToString() ?? string.Empty;
            }
            return result;
        }
        catch
        {
            // If casting or copying fails, return empty array
            return Array.Empty<string>();
        }
    }
}
