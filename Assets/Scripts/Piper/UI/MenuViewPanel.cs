using UnityEngine.UIElements;

namespace PipeWorld.Piper
{
    public class MenuViewPanel : VisualElement {
        public MenuViewPanel() {
            AddToClassList("menu-view-panel");
            pickingMode = PickingMode.Ignore;
            focusable = true;
            tabIndex = -1;
        }

        public virtual void Show() {
            pickingMode = PickingMode.Position;
            AddToClassList("visible");
        }

        public virtual void Hide() {
            RemoveFromClassList("visible");
            pickingMode = PickingMode.Ignore;
        }

        public void FocusFirst() {
            var first = this.Query<VisualElement>()
                .Where(e => e.focusable && e.tabIndex >= 0 && e.enabledInHierarchy)
                .First();
            if (first != null)
                first.Focus();
            else
                Focus();
        }
    }
}
