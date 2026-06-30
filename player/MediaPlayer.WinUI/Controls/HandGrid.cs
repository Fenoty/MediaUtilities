using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Controls;

namespace MediaPlayer.Controls;

public class HandGrid : Grid
{
    public HandGrid()
    {
        ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
    }
}
