# TODO: HelloFresh-Style Meal Planner Implementation Plan

## 1. Analyze HelloFresh UI/UX and define the main features and layout

- Hero/banner section (optional)
- Recipe selection grid: Each card shows image, title, description, tags, etc.
- Selection mechanism: Click to select, visual feedback for selected
- Weekly calendar: 7 days, each can have 1 meal assigned
- Save/submit button for the week’s schedule

## 2. Design the component hierarchy and data flow

- `AppPage` (main page)
  - `RecipeGallery` (fetches and displays recipes)
    - `RecipeCard` (individual recipe)
  - `WeeklyCalendar` (shows 7 days, allows assigning selected recipes)
  - `SelectedMealsSummary` (optional, shows chosen meals)
  - `SaveScheduleButton`

## 3. Define TypeScript types/interfaces for recipes and schedule

- `Recipe`: id, name, image, description, tags, etc.
- `ScheduledMeal`: day (0-6), recipeId
- `ScheduleRequest`: array of `ScheduledMeal`

## 4. Implement API integration for fetching recipes and schedule

- Fetch recipes from `/api/v1/recipes`
- Save schedule to `/api/v1/schedule` (POST)
- (Optional) Fetch existing schedule for the week

## 5. Build the Recipe Gallery (with images, descriptions, etc.)

- Grid layout, responsive
- Show image, title, description, tags
- Selectable (highlight when selected)

## 6. Implement meal selection logic (max 7, one per day)

- Max 7 selections, one per day
- Prevent duplicate days or more than 7
- Visual feedback for selection

## 7. Build a weekly calendar UI for scheduling meals

- 7 columns (Sun-Sat)
- Drag-and-drop or click-to-assign recipe to a day
- Show assigned recipe image/title in each day

## 8. Implement saving the schedule to the API

- Collect selected meals, POST to `/api/v1/schedule`
- Show success/error feedback

## 9. Add loading, error, and empty states

- While fetching recipes/saving schedule
- If no recipes available
- If API errors

## 10. Style the app with Tailwind for a modern, responsive look

- Responsive grid, modern cards, hover/focus states
- Calendar with clear day labels
- Accessible and visually appealing

## 11. Test the user flow and edge cases

- Try selecting more than 7 meals
- Try assigning multiple meals to one day
- Test API errors and empty states
