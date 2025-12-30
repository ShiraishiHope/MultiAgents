"""
Memory Monitor - Python Side
Reports Python memory usage back to C#
"""

import sys
import gc

# Try to import tracemalloc (built-in since Python 3.4)
try:
    import tracemalloc
    TRACEMALLOC_AVAILABLE = True
except ImportError:
    TRACEMALLOC_AVAILABLE = False

# Track if we've started tracemalloc
_tracemalloc_started = False


def start_tracking():
    """
    Call once at startup to begin memory tracking.
    Returns True if tracking started successfully.
    """
    global _tracemalloc_started
    
    if not TRACEMALLOC_AVAILABLE:
        return False
    
    if not _tracemalloc_started:
        tracemalloc.start()
        _tracemalloc_started = True
    
    return True


def get_memory_stats():
    """
    Returns a dictionary with Python memory statistics.
    Called periodically by C# to get current memory state.
    
    Returns dict with:
        - gc_objects: Number of objects tracked by garbage collector
        - gc_garbage: Number of uncollectable objects (circular refs)
        - tracemalloc_current: Current memory usage in bytes (if available)
        - tracemalloc_peak: Peak memory usage in bytes (if available)
        - gc_counts: Tuple of (gen0, gen1, gen2) collection counts
    """
    stats = {}
    
    # GC statistics - always available
    stats['gc_objects'] = len(gc.get_objects())
    stats['gc_garbage'] = len(gc.garbage)
    stats['gc_counts'] = gc.get_count()  # (gen0, gen1, gen2) objects pending collection
    
    # Tracemalloc statistics - if available and started
    if TRACEMALLOC_AVAILABLE and _tracemalloc_started:
        current, peak = tracemalloc.get_traced_memory()
        stats['tracemalloc_current'] = current  # bytes
        stats['tracemalloc_peak'] = peak        # bytes
    else:
        stats['tracemalloc_current'] = -1
        stats['tracemalloc_peak'] = -1
    
    return stats


def force_gc():
    """
    Forces a full garbage collection.
    Useful for getting accurate memory readings.
    Returns number of unreachable objects found.
    """
    return gc.collect()


def get_top_allocations(limit=10):
    """
    Returns the top memory-consuming lines of code.
    Useful for finding where memory is being allocated.
    Only works if tracemalloc is available and started.
    
    Returns list of strings describing top allocations.
    """
    if not TRACEMALLOC_AVAILABLE or not _tracemalloc_started:
        return ["tracemalloc not available"]
    
    snapshot = tracemalloc.take_snapshot()
    top_stats = snapshot.statistics('lineno')
    
    results = []
    for stat in top_stats[:limit]:
        results.append(f"{stat.traceback}: {stat.size / 1024:.1f} KB")
    
    return results


def reset_peak():
    """
    Resets the peak memory tracker.
    Call this after a baseline measurement to track new peaks.
    """
    if TRACEMALLOC_AVAILABLE and _tracemalloc_started:
        tracemalloc.reset_peak()