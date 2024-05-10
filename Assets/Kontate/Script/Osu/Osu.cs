

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.UI;
using com.rfilkov.kinect;

//com.rfilkov.kinect

public class Osu : MonoBehaviour
{
    // Start is called before the first frame update
    [SerializeField] private List<Transform> handLR;
    [SerializeField] private List<Collider> notesColliders;
    [SerializeField] private List<GameObject> notesColliderAppear;

    [SerializeField] private PlayableDirector timeline;
    [SerializeField] private Text scoreText;

    [SerializeField] private GameObject particle;

    private int score = 0;
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        foreach (var handlrit in handLR)
        {
            var all = Physics.OverlapBox(handlrit.position, new Vector3(0.2f, 0.2f, 0.2f));

            foreach (var col in all) 
            {
                if (notesColliders.Contains(col))
                {
                
                    var mat = col.gameObject.GetComponent<Renderer>().material;
                    if (mat.color != new Color(1f,0f,0f,0.3f) && timeline.state == PlayState.Playing)
                    {
                        score += 10;
                        mat.color = new Color(1f,0f,0f,0.3f);
                        Instantiate(particle,col.transform.position,col.transform.rotation);
                    }
                }
            
            }
        }

        scoreText.text = "Score : " + score.ToString();

        int i = 0;
        foreach (var it in notesColliders)
        {
            if (it.gameObject.activeSelf)
            {
                notesColliderAppear[i].gameObject.SetActive(false);
            }
            i++;
        }


        if (Input.GetKeyDown(KeyCode.R))
        {
            score = 0;
            timeline.time = 0;
            timeline.Stop();
            timeline.Evaluate();
        }

        if (Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.Escape))
        {
            Application.Quit();
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (timeline.state == PlayState.Playing)
            {
                timeline.Pause();
            }
            else
            {
                timeline.Play();
            }
        }
    }
}
