using Newtonsoft.Json;

namespace InteractiveWorldMap.Models
{
    public class PinPartPoint
    {
        [JsonProperty("x")]
        public double X { get; set; }

        [JsonProperty("y")]
        public double Y { get; set; }
    }

    public class PinPartImageSize
    {
        [JsonProperty("w")]
        public int Width { get; set; }

        [JsonProperty("h")]
        public int Height { get; set; }
    }

    public class PinPartHeadGeometry
    {
        [JsonProperty("image_size")]
        public PinPartImageSize? ImageSize { get; set; }

        [JsonProperty("local_center")]
        public PinPartPoint LocalCenter { get; set; } = new PinPartPoint();

        [JsonProperty("local_attach")]
        public PinPartPoint? LocalAttach { get; set; }

        [JsonProperty("stub_direction_deg")]
        public double StubDirectionDeg { get; set; }
    }

    public class PinPartShaftSegmentation
    {
        [JsonProperty("tip_cap_length")]
        public double TipCapLength { get; set; }

        [JsonProperty("head_cap_length")]
        public double HeadCapLength { get; set; }

        [JsonProperty("stretch_start_distance")]
        public double StretchStartDistance { get; set; }

        [JsonProperty("stretch_end_distance")]
        public double StretchEndDistance { get; set; }

        [JsonProperty("stretchable_length")]
        public double StretchableLength { get; set; }

        [JsonProperty("minimum_middle_ratio")]
        public double MinimumMiddleRatio { get; set; }
    }

    public class PinPartShaftGeometry
    {
        [JsonProperty("image_size")]
        public PinPartImageSize? ImageSize { get; set; }

        [JsonProperty("local_tip")]
        public PinPartPoint LocalTip { get; set; } = new PinPartPoint();

        [JsonProperty("local_join")]
        public PinPartPoint LocalJoin { get; set; } = new PinPartPoint();

        [JsonProperty("native_angle_deg")]
        public double NativeAngleDeg { get; set; }

        [JsonProperty("native_length")]
        public double NativeLength { get; set; }

        [JsonProperty("segmentation")]
        public PinPartShaftSegmentation? Segmentation { get; set; }
    }

    public class PinPartGeometryEntry
    {
        [JsonProperty("head_file")]
        public string HeadFile { get; set; } = string.Empty;

        [JsonProperty("shaft_file")]
        public string ShaftFile { get; set; } = string.Empty;

        [JsonProperty("head")]
        public PinPartHeadGeometry Head { get; set; } = new PinPartHeadGeometry();

        [JsonProperty("shaft")]
        public PinPartShaftGeometry Shaft { get; set; } = new PinPartShaftGeometry();
    }
}
