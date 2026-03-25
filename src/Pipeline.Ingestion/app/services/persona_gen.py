import json
from openai import AsyncOpenAI
from ..models import Entity, Relationship, Persona, settings

client = AsyncOpenAI(
    api_key=settings.llm_api_key,
    base_url=settings.llm_base_url,
)

PROMPT_TEMPLATE = """
You are building agents for a social simulation grounded in this document.

Document summary: {summary}
Key entities: {entity_list}
Key relationships: {relationship_list}

Generate {count} distinct agent personas. Each persona should be:
- Believable within this world
- Have a distinct personality (not all agree with each other)
- Have a plausible relationship to the entities in the document
- Vary in: age, social status, political leaning, emotional volatility

Return a JSON array of objects, each with:
  id (UUID string), name, backstory (2-3 sentences), traits (list of 3-5 strings), 
  emotionalBaseline (0.0-1.0), socialInfluence (0.0-1.0),
  knownEntities (list of entity ids from above)

Respond ONLY with valid JSON. No preamble or explanation.
"""

async def generate(entities: list[Entity], relationships: list[Relationship], count: int, seed_content: str) -> list[Persona]:
    """Generates personas using LLM."""
    
    entity_str = ", ".join([f"{e.name} ({e.label})" for e in entities[:50]]) # Cap at 50 for prompt context
    rel_str = ", ".join([f"{r.source_id} -[{r.relation_type}]-> {r.target_id}" for r in relationships[:50]])
    
    summary = seed_content[:1500] + ("..." if len(seed_content) > 1500 else "")
    
    prompt = PROMPT_TEMPLATE.format(
        summary=summary,
        entity_list=entity_str,
        relationship_list=rel_str,
        count=count
    )

    response = await client.chat.completions.create(
        model=settings.llm_model_name,
        messages=[
            {"role": "user", "content": prompt}
        ],
        temperature=0.7,
        # Force JSON response if supported by OpenAI-compatible endpoint
        response_format={"type": "json_object"} if "gpt" in settings.llm_model_name.lower() else None
    )

    content = response.choices[0].message.content.strip()
    
    # Strip markdown code blocks if LLM still added them
    if content.startswith("```json"):
        content = content[7:-3]
    elif content.startswith("```"):
        content = content[3:-3]
        
    # The prompt asks for a JSON array, but openai response_format requires an object.
    # We parse whatever JSON it returns natively
    try:
        data = json.loads(content)
        
        # If the LLM returned an object with a list inside like {"personas": [...]}
        if isinstance(data, dict):
            # find the first list value
            for val in data.values():
                if isinstance(val, list):
                    data = val
                    break
                    
        # Map to Pydantic
        personas = []
        for p in data:
            personas.append(Persona(**p))
            
        return personas
    except json.JSONDecodeError as e:
        print(f"Failed to parse LLM response: {content}")
        raise ValueError("LLM returned malformed JSON") from e
