using Xunit;

// Several legacy disk-cache tests intentionally use shared AppData directories.
// Serialize test classes so cache cleanup and round-trip assertions cannot race.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
