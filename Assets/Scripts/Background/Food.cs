using UnityEngine;

public class FoodPlate : MonoBehaviour
{
    #region Static Registry
    private static System.Collections.Generic.Dictionary<string, FoodPlate> foodRegistry =
        new System.Collections.Generic.Dictionary<string, FoodPlate>();

    public static FoodPlate GetFoodByID(string id)
    {
        foodRegistry.TryGetValue(id, out FoodPlate food);
        return food;
    }

    public static FoodPlate[] GetAllFood()
    {
        FoodPlate[] allFood = new FoodPlate[foodRegistry.Count];
        foodRegistry.Values.CopyTo(allFood, 0);
        return allFood;
    }
    #endregion

    #region References
    [Header("Plate Visuals (Child Objects)")]
    [SerializeField] private GameObject fullPlate;
    [SerializeField] private GameObject halfPlate;
    [SerializeField] private GameObject emptyPlate;
    #endregion

    #region State
    [Header("Food Amount")]
    [SerializeField] private int foodCount = 2;

    private string instanceID;

    public int FoodCount => foodCount;
    public bool IsEmpty => foodCount <= 0;
    public string InstanceID => instanceID;
    #endregion

    #region Unity Lifecycle
    void Awake()
    {
        instanceID = $"Food_{Mathf.Abs(GetInstanceID())}";

        if (!foodRegistry.ContainsKey(instanceID))
        {
            foodRegistry[instanceID] = this;
        }
    }

    void Start()
    {
        UpdateVisuals();
    }

    void OnDestroy()
    {
        foodRegistry.Remove(instanceID);
    }
    #endregion

    #region Public Interface
    public bool Consume()
    {
        if (foodCount <= 0)
            return false;

        foodCount--;
        UpdateVisuals();
        return true;
    }

    public void Refill()
    {
        foodCount = 2;
        UpdateVisuals();
    }
    #endregion

    #region Visual Management
    private void UpdateVisuals()
    {
        if (fullPlate != null)
            fullPlate.SetActive(foodCount == 2);

        if (halfPlate != null)
            halfPlate.SetActive(foodCount == 1);

        if (emptyPlate != null)
            emptyPlate.SetActive(foodCount <= 0);
    }
    #endregion
}