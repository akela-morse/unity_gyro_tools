using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Assertions;
using System.Collections;

public class GyroRotatedObject : MonoBehaviour
{
    const float InputUpdateDelayMS = 0.001f;
    RotationInputs rotationInputs;

    #region Input Action Syntatic Sugar
    /// <summary>
    /// Method to bypass potential type unsafety when using input action callbacks
    /// </summary>
    /// <typeparam name="T">Type you want to extract from the context</typeparam>
    /// <param name="context">Callback context parameter from input action callback</param>
    /// <returns></returns>
    static T SafelyReadCallbackValue<T>(InputAction.CallbackContext context) where T : struct
    {
        Assert.IsTrue(
            context.valueType == typeof(T),
            $"InputAction '{context.action.name}' expected value type {typeof(T).Name}, " +
            $"but was {context.valueType?.Name ?? "null"}"
        );

        return context.ReadValue<T>();
    }
    void GyroToRotation(InputAction.CallbackContext context) => GyroToRotation(SafelyReadCallbackValue<Vector3>(context));

    #endregion

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rotationInputs = new RotationInputs();
        rotationInputs.Imu.Enable();
        rotationInputs.Imu.Gyro.performed += GyroToRotation;

        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
        
        StartCoroutine(UpdateInputSystem(new WaitForSeconds(InputUpdateDelayMS)));

        Application.quitting += OnQuit;
        
    }

    IEnumerator UpdateInputSystem(WaitForSeconds delay)
    {
        while(true)
        {  
            InputSystem.Update();       
            yield return delay;
        }
    }

    void OnQuit()
    {
        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
    }



    void GyroToRotation(Vector3 gyroscope)
    {
        if(Mathf.Abs(gyroscope.x) > 0.01f && Mathf.Abs(gyroscope.y) > 0.01f) 
            gameObject.transform.Rotate(gyroscope * Mathf.Rad2Deg * Time.deltaTime);
    }

    

}
