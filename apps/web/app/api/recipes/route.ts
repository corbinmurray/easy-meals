import type { Recipe } from "@/types/meal-planner";
import { NextResponse } from "next/server";

/**
 * GET /api/recipes
 * 
 * Server-side proxy to the internal .NET API.
 * This keeps the .NET API private and unexposed to the public internet.
 * 
 * Browser → Next.js API Route → .NET API (internal)
 */
export async function GET() {
  try {
    // API_URL is server-side only, not exposed to browser
    const apiUrl = process.env.API_URL || "http://localhost:5000";
    
    const response = await fetch(`${apiUrl}/api/recipes`, {
      headers: {
        "Content-Type": "application/json",
      },
      // Disable caching for now to always get fresh data
      cache: "no-store",
    });

    if (!response.ok) {
      throw new Error(`API responded with status: ${response.status}`);
    }

    const recipes: Recipe[] = await response.json();
    return NextResponse.json(recipes);
  } catch (error) {
    console.error("Failed to fetch recipes from internal API:", error);
    
    // Return empty array with error status
    // TODO: Implement proper error handling UI
    return NextResponse.json(
      { error: "Failed to load recipes", recipes: [] },
      { status: 500 }
    );
  }
}
