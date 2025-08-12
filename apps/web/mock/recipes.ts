import type { Recipe } from "@/types/meal-planner";

export const mockRecipes: Recipe[] = [
  {
    id: "1",
    name: "Lemon Herb Chicken",
    image: "https://images.unsplash.com/photo-1504674900247-0877df9cc836?auto=format&fit=crop&w=400&q=80",
    description: "Juicy chicken breast with a zesty lemon herb marinade, served with roasted potatoes and green beans.",
    tags: ["Chicken", "Gluten-Free", "Dinner"],
  },
  {
    id: "2",
    name: "Vegetarian Tacos",
    image: "https://images.unsplash.com/photo-1519864600265-abb23847ef2c?auto=format&fit=crop&w=400&q=80",
    description: "Crispy corn tortillas filled with spiced black beans, avocado, and fresh salsa.",
    tags: ["Vegetarian", "Mexican", "Quick"],
  },
  {
    id: "3",
    name: "Salmon Poke Bowl",
    image: "https://images.unsplash.com/photo-1464306076886-debca5e8a6b0?auto=format&fit=crop&w=400&q=80",
    description: "Fresh salmon cubes over sushi rice with edamame, cucumber, and spicy mayo.",
    tags: ["Seafood", "Healthy", "Lunch"],
  },
  {
    id: "4",
    name: "Classic Beef Burger",
    image: "https://images.unsplash.com/photo-1550547660-d9450f859349?auto=format&fit=crop&w=400&q=80",
    description: "Grilled beef patty with cheddar, lettuce, tomato, and house sauce on a toasted bun.",
    tags: ["Beef", "American", "Comfort Food"],
  },
  {
    id: "5",
    name: "Pasta Primavera",
    image: "https://images.unsplash.com/photo-1502741338009-cac2772e18bc?auto=format&fit=crop&w=400&q=80",
    description: "Penne pasta tossed with seasonal vegetables and a light garlic cream sauce.",
    tags: ["Vegetarian", "Italian", "Pasta"],
  },
];
