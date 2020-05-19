﻿using System.Collections.ObjectModel;
using System.Reactive;
using System.Threading.Tasks;
using Alex.GuiDebugger.Models;
using Alex.GuiDebugger.Services;
using Dock.Model.Controls;
using DynamicData;
using ReactiveUI;

namespace Alex.GuiDebugger.ViewModels.Documents
{
    public class ElementTreeDocument : Document
    {
        public ElementTreeItem ElementTreeItem { get; }

        public ObservableCollection<ElementTreeItemProperty> Properties { get; }

        public ReactiveCommand<Unit, Unit> RefreshPropertiesCommand { get; }

        public ElementTreeDocument(ElementTreeItem elementTreeItem)
        {
            ElementTreeItem = elementTreeItem;
            Properties = new ObservableCollection<ElementTreeItemProperty>();

            Title = ElementTreeItem.ElementType;

            RefreshPropertiesCommand = ReactiveCommand.CreateFromTask(RefreshProperties);
        }

        private async Task RefreshProperties()
        {
            var newItems = await AlexGuiDebuggerInteraction.Instance.GetElementTreeItemProperties(ElementTreeItem.Id);
            Properties.Clear();
            Properties.AddRange(newItems);
        }

        public override void OnSelected()
        {
            base.OnSelected();
            AlexGuiDebuggerInteraction.Instance.HighlightElement(ElementTreeItem.Id);
        }
    }
}
