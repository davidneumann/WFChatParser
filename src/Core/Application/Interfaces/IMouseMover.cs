using System.Drawing;

namespace Application.Interfaces
{
    public interface IMouseMover
    {
        void ScrollUp();
        void ScrollDown();
        void MoveTo(int x, int y);
        void Click(int x, int y);
        /// <summary>
        /// Click and drag with left click
        /// </summary>
        /// <param name="startPoint"></param>
        /// <param name="finalPoint"></param>
        /// <param name="timeInMS"></param>
        void ClickAndDrag(Point startPoint, Point finalPoint, int timeInMS);
    }
}