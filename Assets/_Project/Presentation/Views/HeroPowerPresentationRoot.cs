using UnityEngine;

namespace CoH.Presentation
{
    /// <summary>
    /// Serialized ownership record for every scene object created by the Hero
    /// Power installer. The presentation spans the HUD canvas and the world
    /// drag layer, so a Transform subtree cannot own all of it without changing
    /// its rendering semantics; this logical root keeps that ownership explicit.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeroPowerPresentationRoot : MonoBehaviour
    {
        [SerializeField] private GameObject heroPower;
        [SerializeField] private GameObject choices;
        [SerializeField] private GameObject choiceCardAnchor;
        [SerializeField] private GameObject choiceBackdrop;

        public GameObject HeroPower => heroPower;
        public GameObject Choices => choices;
        public GameObject ChoiceCardAnchor => choiceCardAnchor;
        public GameObject ChoiceBackdrop => choiceBackdrop;

        public void Bind(
            GameObject heroPowerObject,
            GameObject choicesObject,
            GameObject choiceCardAnchorObject,
            GameObject choiceBackdropObject)
        {
            heroPower = heroPowerObject;
            choices = choicesObject;
            choiceCardAnchor = choiceCardAnchorObject;
            choiceBackdrop = choiceBackdropObject;
        }
    }
}
