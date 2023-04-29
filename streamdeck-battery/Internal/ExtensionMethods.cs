using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Automation;

namespace Battery.Internal
{
    static class AutomationElementHelpers
    {
        public static AutomationElement Find(this AutomationElement root, string name)
        {
            return root.FindFirst(
             TreeScope.Descendants,
             new PropertyCondition(AutomationElement.NameProperty, name));
        }

        public static IEnumerable<AutomationElement> EnumChildButtons(this AutomationElement parent)
        {
            return parent == null ? Enumerable.Empty<AutomationElement>()
                                  : parent.FindAll(TreeScope.Children,
              new PropertyCondition(AutomationElement.ControlTypeProperty,
                                    ControlType.Button)).Cast<AutomationElement>();
        }

        public static bool InvokeButton(this AutomationElement button)
        {
            var invokePattern = button.GetCurrentPattern(InvokePattern.Pattern)
                               as InvokePattern;
            if (invokePattern != null)
            {
                invokePattern.Invoke();
            }
            return invokePattern != null;
        }

        public static AutomationElement GetTopLevelElement(this AutomationElement element)
        {
            AutomationElement parent;
            while ((parent = TreeWalker.ControlViewWalker.GetParent(element)) !=
                 AutomationElement.RootElement)
            {
                element = parent;
            }
            return element;
        }
    }
}