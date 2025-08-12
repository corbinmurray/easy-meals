import type { Recipe } from "@/types/meal-planner";
import {
  Avatar,
  AvatarFallback,
  AvatarImage,
} from "@workspace/ui/components/avatar";
import { Badge } from "@workspace/ui/components/badge";
import { Button } from "@workspace/ui/components/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardFooter,
  CardHeader,
  CardTitle,
} from "@workspace/ui/components/card";

interface RecipeCardProps {
  recipe: Recipe;
  selected: boolean;
  onSelect: () => void;
}

export default function RecipeCard({
  recipe,
  selected,
  onSelect,
}: RecipeCardProps) {
  return (
    <Card
      className={`flex flex-col h-full min-h-[340px] max-h-[400px] transition-shadow ${selected ? "ring-2 ring-primary" : "hover:shadow-lg"}`}
    >
      {/* 1. Header: Avatar and Name */}
      <CardHeader className="flex flex-row items-center gap-4 pb-2">
        <Avatar className="size-16">
          <AvatarImage src={recipe.image} alt={recipe.name} />
          <AvatarFallback>
            {recipe.name.slice(0, 2).toUpperCase()}
          </AvatarFallback>
        </Avatar>
        <CardTitle className="text-lg leading-tight">{recipe.name}</CardTitle>
      </CardHeader>

      {/* 2. Tags row */}
      <div className="flex flex-row flex-wrap gap-2 px-6 pb-2 min-h-[32px]">
        {recipe.tags?.map((tag) => (
          <Badge key={tag} variant="secondary">
            {tag}
          </Badge>
        ))}
      </div>

      {/* 3. Description */}
      <CardContent className="flex-1 px-6 pb-2">
        <CardDescription className="text-sm text-muted-foreground line-clamp-3">
          {recipe.description}
        </CardDescription>
      </CardContent>

      {/* 4. Button at bottom */}
      <CardFooter className="mt-auto px-6 pb-4">
        <Button
          variant={selected ? "default" : "outline"}
          onClick={onSelect}
          className="w-full"
        >
          {selected ? "Selected" : "Select"}
        </Button>
      </CardFooter>
    </Card>
  );
}
