# Setting Node History Feature Plan

## Overview
Implement a breadcrumb-style history system for the SettingsManagerWindow that tracks the last 10 selected setting nodes, allowing users to navigate back and forward through their selection history.

## Requirements
- Track last 10 selected setting nodes
- Support back/forward navigation
- Update history on each node selection
- Prevent duplicates (move existing entries to most recent)
- Editor-only feature (no runtime impact)
- Persist history during editor session
- Prepare for UI display (future task)

## Implementation Details

### 1. Create History Manager Class
**File**: `Assets/ScriptableSettings/Editor/SettingNodeHistory.cs`
- Manage circular buffer of 10 node references
- Track current position for back/forward navigation
- Handle node selection events
- Provide navigation methods

### 2. Data Structure
```csharp
public class SettingNodeHistoryEntry
{
    public string NodePath { get; set; }
    public string NodeId { get; set; }
    public DateTime LastAccessTime { get; set; }
}
```

### 3. Core Features
- **Add to History**: When node selected, check if exists, move to front or add new
- **Navigate Back**: Move to previous node in history
- **Navigate Forward**: Move to next node in history (if available)
- **Clear History**: Reset all entries
- **Get Display List**: Return ordered list for UI display

### 4. Integration Points
- Hook into `SettingsManagerWindow.OnSelectionChanged`
- Store history in EditorPrefs for session persistence
- Add keyboard shortcuts (Alt+Left/Right for back/forward)

### 5. Edge Cases to Handle
- Node deletion while in history
- Node path changes
- Invalid/null references
- History overflow (>10 items)

## Technical Approach

### Storage Strategy
- Use EditorPrefs with JSON serialization
- Key: "ScriptableSettings.NodeHistory"
- Store node paths and IDs for resilience

### Event Flow
1. User selects node in tree view
2. SettingsManagerWindow fires selection event
3. HistoryManager captures event
4. Check for duplicates, update position
5. Store in EditorPrefs
6. Update navigation button states

### API Design
```csharp
public interface ISettingNodeHistory
{
    void AddNode(SettingNode node);
    bool CanGoBack { get; }
    bool CanGoForward { get; }
    void NavigateBack();
    void NavigateForward();
    void Clear();
    IReadOnlyList<SettingNodeHistoryEntry> GetHistory();
}
```

## File Structure
```
Assets/ScriptableSettings/Editor/
├── History/
│   ├── SettingNodeHistory.cs
│   ├── SettingNodeHistoryEntry.cs
│   └── ISettingNodeHistory.cs
└── SettingsManagerWindow.cs (modifications)
```

## Testing Strategy
- Unit tests for history management logic
- Test circular buffer behavior
- Test duplicate handling
- Test persistence across editor sessions
- Integration tests with SettingsManagerWindow

## Future UI Considerations (Next Task)
- Dropdown showing recent 10 items
- Back/Forward buttons in toolbar
- Keyboard shortcuts display
- Visual breadcrumb trail
- Quick access menu

## Implementation Order
1. Create history data structures
2. Implement core history manager
3. Add EditorPrefs persistence
4. Integrate with SettingsManagerWindow
5. Add keyboard shortcuts
6. Write comprehensive tests
7. Prepare UI hooks for next phase

## Success Criteria
- History tracks last 10 unique selections
- Back/forward navigation works correctly
- No duplicates in history
- Persists during editor session
- No impact on runtime code
- Clean integration with existing editor
- All tests passing