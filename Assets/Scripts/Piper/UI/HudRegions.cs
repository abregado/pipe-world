using UnityEngine.UIElements;

namespace PipeWorld.Piper
{
    public static class HudRegions {
        public static void Build(VisualElement parent) {
            parent.Add(Region("MenuLayer"));

            var layer1 = Layer("Layer1", parent);
            var topRow = Region(null, layer1);
            topRow.style.flexDirection = FlexDirection.Row;
            topRow.style.height = Length.Percent(50);
            var topLeft = Region("top-left", topRow);
            topLeft.style.width = Length.Percent(50);
            topLeft.style.flexDirection = FlexDirection.Column;
            topLeft.style.alignItems = Align.FlexStart;
            var topRight = Region("top-right", topRow);
            topRight.style.width = Length.Percent(50);
            topRight.style.alignItems = Align.Auto;
            var bottomCenter = Region("bottom-center", layer1);
            bottomCenter.style.flexDirection = FlexDirection.ColumnReverse;
            bottomCenter.style.height = Length.Percent(50);
            bottomCenter.style.alignItems = Align.Center;
            bottomCenter.style.justifyContent = Justify.FlexStart;

            var layer2 = Layer("Layer2", parent);
            var topCenter = Region("top-center", layer2);
            topCenter.style.alignItems = Align.Center;
            topCenter.style.height = Length.Percent(50);
            var bottomRow = Region(null, layer2);
            bottomRow.style.flexDirection = FlexDirection.Row;
            bottomRow.style.height = Length.Percent(50);
            var bottomLeft = Region("bottom-left", bottomRow);
            bottomLeft.style.alignItems = Align.FlexEnd;
            bottomLeft.style.justifyContent = Justify.FlexStart;
            bottomLeft.style.flexDirection = FlexDirection.Column;
            bottomLeft.style.width = Length.Percent(50);
            var bottomRight = Region("bottom-right", bottomRow);
            bottomRight.style.width = Length.Percent(50);
            bottomRight.style.alignItems = Align.FlexStart;
            bottomRight.style.alignSelf = Align.Auto;

            var crosshairLayer = Region("CrossHairLayer", parent);
            crosshairLayer.style.flexDirection = FlexDirection.Row;
            crosshairLayer.style.alignItems = Align.Center;

            var layer3 = Layer("Layer3", parent);
            layer3.style.flexDirection = FlexDirection.Row;
            var fullLeft = Region("full-left", layer3);
            fullLeft.style.width = Length.Percent(50);
            fullLeft.style.flexDirection = FlexDirection.Column;
            fullLeft.style.alignItems = Align.FlexStart;
            var fullRight = Region("full-right", layer3);
            fullRight.style.width = Length.Percent(50);
            fullRight.style.alignItems = Align.Auto;
        }

        private static VisualElement Region(string name, VisualElement parent = null) {
            var e = new VisualElement();
            if (name != null) e.name = name;
            e.style.flexGrow = 1;
            e.style.backgroundColor = new StyleColor(UnityEngine.Color.clear);
            parent?.Add(e);
            return e;
        }

        private static VisualElement Layer(string name, VisualElement parent) {
            var e = Region(name, parent);
            e.style.flexShrink = 0;
            e.style.position = Position.Absolute;
            e.style.width = Length.Percent(100);
            e.style.height = Length.Percent(100);
            e.style.paddingLeft = 20;
            e.style.paddingRight = 20;
            e.style.paddingTop = 20;
            e.style.paddingBottom = 20;
            return e;
        }
    }
}
