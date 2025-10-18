using System;
using System.Collections.Generic;
using System.Drawing;
using Memux.Core.Models;

namespace Memux.Perception;

/// <summary>
/// Detects regions of the screen that have changed between frames
/// Used to optimize OCR by only processing changed areas
/// </summary>
public class DirtyRegionDetector
{
    private byte[]? _previousFrame;
    private int _previousWidth;
    private int _previousHeight;
    private readonly int _threshold;
    private readonly int _minRegionSize;

    public DirtyRegionDetector(int threshold = 30, int minRegionSize = 10)
    {
        _threshold = threshold;
        _minRegionSize = minRegionSize;
    }

    /// <summary>
    /// Compare current frame with previous frame and return changed regions
    /// </summary>
    public List<Rectangle> CompareFrames(byte[] currentFrame, int width, int height)
    {
        // First frame - everything is dirty
        if (_previousFrame == null || _previousWidth != width || _previousHeight != height)
        {
            _previousFrame = new byte[currentFrame.Length];
            Array.Copy(currentFrame, _previousFrame, currentFrame.Length);
            _previousWidth = width;
            _previousHeight = height;
            return new List<Rectangle> { new Rectangle(0, 0, width, height) };
        }

        var dirtyPixels = new bool[width * height];
        int dirtyCount = 0;

        // Find changed pixels
        unsafe
        {
            fixed (byte* curr = currentFrame)
            fixed (byte* prev = _previousFrame)
            {
                for (int i = 0; i < width * height; i++)
                {
                    int pixelIdx = i * 4;
                    
                    // Compare BGR channels (skip alpha)
                    int diffB = Math.Abs(curr[pixelIdx + 0] - prev[pixelIdx + 0]);
                    int diffG = Math.Abs(curr[pixelIdx + 1] - prev[pixelIdx + 1]);
                    int diffR = Math.Abs(curr[pixelIdx + 2] - prev[pixelIdx + 2]);
                    
                    int totalDiff = diffB + diffG + diffR;
                    
                    if (totalDiff > _threshold)
                    {
                        dirtyPixels[i] = true;
                        dirtyCount++;
                    }
                }
            }
        }

        // Update previous frame
        Array.Copy(currentFrame, _previousFrame, currentFrame.Length);

        // If nothing changed, return empty list
        if (dirtyCount == 0)
        {
            return new List<Rectangle>();
        }

        // Group dirty pixels into rectangular regions
        return GroupIntoRectangles(dirtyPixels, width, height);
    }

    /// <summary>
    /// Expand dirty regions to include any OCR bounding boxes that overlap
    /// </summary>
    public List<Rectangle> ExpandRegionsForBoundingBoxes(List<Rectangle> dirtyRegions, List<OcrResult>? previousResults)
    {
        if (previousResults == null || previousResults.Count == 0)
            return dirtyRegions;

        var expandedRegions = new List<Rectangle>();

        foreach (var dirtyRegion in dirtyRegions)
        {
            var expanded = dirtyRegion;

            // Check each OCR result for intersection
            foreach (var ocrResult in previousResults)
            {
                var bbox = new Rectangle(
                    (int)ocrResult.BoundingBox.X,
                    (int)ocrResult.BoundingBox.Y,
                    (int)ocrResult.BoundingBox.Width,
                    (int)ocrResult.BoundingBox.Height
                );

                // If this bounding box intersects the dirty region, expand to include it
                if (expanded.IntersectsWith(bbox))
                {
                    expanded = Rectangle.Union(expanded, bbox);
                }
            }

            expandedRegions.Add(expanded);
        }

        // Merge overlapping expanded regions
        return MergeOverlappingRectangles(expandedRegions);
    }

    private List<Rectangle> GroupIntoRectangles(bool[] dirtyPixels, int width, int height)
    {
        var regions = new List<Rectangle>();
        var visited = new bool[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int idx = y * width + x;
                
                if (dirtyPixels[idx] && !visited[idx])
                {
                    // Found a dirty pixel - expand to rectangle
                    var rect = ExpandToRectangle(dirtyPixels, visited, x, y, width, height);
                    
                    if (rect.Width >= _minRegionSize && rect.Height >= _minRegionSize)
                    {
                        regions.Add(rect);
                    }
                }
            }
        }

        return MergeOverlappingRectangles(regions);
    }

    private Rectangle ExpandToRectangle(bool[] dirtyPixels, bool[] visited, int startX, int startY, int width, int height)
    {
        int minX = startX;
        int minY = startY;
        int maxX = startX;
        int maxY = startY;

        // Simple flood fill to find connected dirty pixels
        var queue = new Queue<(int x, int y)>();
        queue.Enqueue((startX, startY));
        visited[startY * width + startX] = true;

        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            
            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);

            // Check 4-connected neighbors
            int[][] offsets = { new[] { -1, 0 }, new[] { 1, 0 }, new[] { 0, -1 }, new[] { 0, 1 } };
            
            foreach (var offset in offsets)
            {
                int nx = x + offset[0];
                int ny = y + offset[1];
                
                if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                {
                    int nidx = ny * width + nx;
                    
                    if (dirtyPixels[nidx] && !visited[nidx])
                    {
                        visited[nidx] = true;
                        queue.Enqueue((nx, ny));
                    }
                }
            }
        }

        return new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    private List<Rectangle> MergeOverlappingRectangles(List<Rectangle> rectangles)
    {
        if (rectangles.Count <= 1)
            return rectangles;

        var merged = new List<Rectangle>();
        var used = new bool[rectangles.Count];

        for (int i = 0; i < rectangles.Count; i++)
        {
            if (used[i]) continue;

            var current = rectangles[i];
            bool changed = true;

            while (changed)
            {
                changed = false;
                
                for (int j = 0; j < rectangles.Count; j++)
                {
                    if (i == j || used[j]) continue;

                    if (current.IntersectsWith(rectangles[j]))
                    {
                        current = Rectangle.Union(current, rectangles[j]);
                        used[j] = true;
                        changed = true;
                    }
                }
            }

            merged.Add(current);
        }

        return merged;
    }

    /// <summary>
    /// Reset the detector (e.g., when switching windows)
    /// </summary>
    public void Reset()
    {
        _previousFrame = null;
    }
}

