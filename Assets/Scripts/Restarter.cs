using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CarWars;

public class Restarter : MonoBehaviour {

    void OnTriggerStay2D(Collider2D collision)
    {
        if(collision.tag == "Player")
        {
            var car = collision.GetComponent<CarController>();
            car.CurrentReward = -500;
            car.Reset();
        }
        Debug.Log("TriggerStay");
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        Debug.Log("TriggerEnter");
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        Debug.Log("TriggerExit");

    }
}
