using Content.Client.Guidebook.Controls;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.RichText;

namespace Content.Client.UserInterface.ControlExtensions;

public static class ControlExtension
{
    // WWDP EDIT START - +bool includeChildClasses
    public static List<T> GetControlOfType<T>(this Control parent, bool includeChildClasses = false) where T : Control
    {
        return parent.GetControlOfType<T>(typeof(T).Name, false, includeChildClasses);
    }
    public static List<T> GetControlOfType<T>(this Control parent, string childType, bool includeChildClasses = false) where T : Control
    {
        return parent.GetControlOfType<T>(childType, false, includeChildClasses);
    }

    public static List<T> GetControlOfType<T>(this Control parent, bool fullTreeSearch, bool includeChildClasses = false) where T : Control
    {
        return parent.GetControlOfType<T>(typeof(T).Name, fullTreeSearch, includeChildClasses);
    }
    // WWDP EDIT END
    
    public static List<T> GetControlOfType<T>(this Control parent, string childType, bool fullTreeSearch, bool includeChildClasses = false) where T : Control // WWDP EDIT
    {
        List<T> controlList = new List<T>();

        foreach (var child in parent.Children)
        {
            var isType = includeChildClasses ? child is T : child.GetType().Name == childType; // WWDP EDIT
            var hasChildren = child.ChildCount > 0;

            var searchDeeper = hasChildren && !isType;

            if (isType)
            {
                controlList.Add((T) child);
            }

            if (fullTreeSearch || searchDeeper)
            {
                controlList.AddRange(child.GetControlOfType<T>(childType, fullTreeSearch, includeChildClasses)); // WWDP EDIT
            }
        }

        return controlList;
    }

    public static List<ISearchableControl> GetSearchableControls(this Control parent, bool fullTreeSearch = false)
    {
        List<ISearchableControl> controlList = new List<ISearchableControl>();

        foreach (var child in parent.Children)
        {
            var hasChildren = child.ChildCount > 0;
            var searchDeeper = hasChildren && child is not ISearchableControl;

            if (child is ISearchableControl searchableChild)
            {
                controlList.Add(searchableChild);
            }

            if (fullTreeSearch || searchDeeper)
            {
                controlList.AddRange(child.GetSearchableControls(fullTreeSearch));
            }
        }

        return controlList;
    }

    public static bool ChildrenContainText(this Control parent, string search)
    {
        var labels = parent.GetControlOfType<Label>();
        var richTextLabels = parent.GetControlOfType<RichTextLabel>();

        foreach (var label in labels)
        {
            if (label.Text != null && label.Text.Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        foreach (var label in richTextLabels)
        {
            var text = label.GetMessage();

            if (text != null && text.Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
